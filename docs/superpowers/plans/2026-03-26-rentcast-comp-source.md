# RentCast Comp Source — Implementation Plan

**Date:** 2026-03-26
**Spec:** `docs/superpowers/specs/2026-03-26-rentcast-comp-source-design.md`
**Status:** Ready to implement

---

## Agentic Worker Instruction

This plan is designed for subagent-driven development. Each phase's tasks are independent and
can be executed in parallel by separate subagents. The phase boundary is the only hard sequencing
constraint — do not start Phase 2 until all Phase 1 tasks are merged.

**Spawn pattern:**
```
Phase 1: Launch Tasks 1–4 in parallel (4 agents simultaneously)
Phase 2: Launch Tasks 5–10 in parallel (6 agents simultaneously), after Phase 1 completes
Phase 3: Launch Tasks 11–12 in parallel (2 agents), after Phase 2 completes
Phase 4: Launch Tasks 13–17 in parallel (5 agents), after Phase 3 completes
```

---

## Overview

Replace the ScraperAPI-based `ScraperCompSource` (which uses Zillow/Redfin/Realtor.com HTML
scraping + Claude extraction) with a single `RentCastCompSource` backed by the RentCast API.
This restores the broken CMA pipeline and reduces per-CMA cost from ~$0.22 to ~$0.09.

**Files to create:**
- `apps/api/RealEstateStar.Domain/Cma/Interfaces/IRentCastClient.cs`
- `apps/api/RealEstateStar.Domain/Cma/Models/RentCastValuation.cs`
- `apps/api/RealEstateStar.Domain/Shared/RentCastDiagnostics.cs`
- `apps/api/RealEstateStar.Clients.RentCast/RealEstateStar.Clients.RentCast.csproj`
- `apps/api/RealEstateStar.Clients.RentCast/RentCastOptions.cs`
- `apps/api/RealEstateStar.Clients.RentCast/RentCastClient.cs`
- `apps/api/RealEstateStar.Workers.Cma/RentCastCompSource.cs`
- `apps/api/RealEstateStar.Api/Health/RentCastHealthCheck.cs`
- `apps/api/tests/RealEstateStar.Clients.RentCast.Tests/RealEstateStar.Clients.RentCast.Tests.csproj`
- `apps/api/tests/RealEstateStar.Clients.RentCast.Tests/RentCastClientTests.cs`
- `apps/api/tests/RealEstateStar.Workers.Cma.Tests/RentCastCompSourceTests.cs`

**Files to modify:**
- `apps/api/RealEstateStar.Domain/Cma/Models/Comp.cs` — add `RentCast` to `CompSource` enum
- `apps/api/RealEstateStar.Api/Diagnostics/OpenTelemetryExtensions.cs` — register RentCast meters/sources
- `apps/api/RealEstateStar.Api/Infrastructure/PollyPolicies.cs` — add `AddRentCastResilience` extension
- `apps/api/RealEstateStar.Api/Program.cs` — DI wiring, remove scraper loop, add startup log
- `apps/api/RealEstateStar.Api/appsettings.json` — remove `Pipeline:Cma:Sources`, add `RentCast` block
- `apps/api/RealEstateStar.Api/RealEstateStar.Api.csproj` — add `Clients.RentCast` project reference
- `apps/api/RealEstateStar.Api.sln` — add `Clients.RentCast` and `Clients.RentCast.Tests` projects
- `apps/api/Dockerfile` — add COPY line for `Clients.RentCast.csproj`
- `apps/api/tests/RealEstateStar.Architecture.Tests/DependencyTests.cs` — `[InlineData]` for new project
- `apps/api/tests/RealEstateStar.Architecture.Tests/DiRegistrationTests.cs` — add `IRentCastClient` resolution test
- `apps/api/tests/RealEstateStar.Architecture.Tests/RealEstateStar.Architecture.Tests.csproj` — add project reference
- `.github/workflows/deploy-api.yml` — add `RENTCAST_API_KEY` secret injection
- `CLAUDE.md` — add `Clients.RentCast` to monorepo structure table
- `.claude/CLAUDE.md` — add `Clients.RentCast` to project list

**Files to delete:**
- `apps/api/RealEstateStar.Workers.Cma/ScraperCompSource.cs`
- `apps/api/tests/RealEstateStar.Workers.Cma.Tests/ScraperCompSourceTests.cs`

---

## Phase 1: Foundation (all independent — run Tasks 1–4 in parallel)

---

### - [ ] Task 1: Domain models — `IRentCastClient`, `RentCastValuation`, `RentCastComp`, `CompSource.RentCast`

**Touches:** Domain only. No dependencies on other Phase 1 tasks.

#### Step 1a: Add `RentCast` to `CompSource` enum

**Modify:** `apps/api/RealEstateStar.Domain/Cma/Models/Comp.cs`

```csharp
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CompSource
{
    Zillow,
    RealtorCom,
    Redfin,
    RentCast      // new
}
```

Note: `CompAggregator.Deduplicate` orders by `(int)c.Source` when deduplicating. `RentCast` gets
the highest ordinal (3). With only one source registered post-change, this ordering is irrelevant
but must not break the build.

#### Step 1b: Create `IRentCastClient`

**Create:** `apps/api/RealEstateStar.Domain/Cma/Interfaces/IRentCastClient.cs`

```csharp
namespace RealEstateStar.Domain.Cma.Interfaces;

public interface IRentCastClient
{
    Task<RentCastValuation?> GetValuationAsync(string address, CancellationToken ct);
}
```

Placed in `Domain/Cma/Interfaces/` alongside `ICompSource`, `ICompAggregator`, `ICmaAnalyzer`, etc.

#### Step 1c: Create `RentCastValuation` and `RentCastComp`

**Create:** `apps/api/RealEstateStar.Domain/Cma/Models/RentCastValuation.cs`

```csharp
namespace RealEstateStar.Domain.Cma.Models;

public record RentCastValuation
{
    public required decimal Price { get; init; }
    public required decimal PriceRangeLow { get; init; }
    public required decimal PriceRangeHigh { get; init; }
    public required IReadOnlyList<RentCastComp> Comparables { get; init; }
}

public record RentCastComp
{
    public required string FormattedAddress { get; init; }
    public string? PropertyType { get; init; }
    public int? Bedrooms { get; init; }
    public decimal? Bathrooms { get; init; }   // decimal because RentCast returns 2.5
    public int? SquareFootage { get; init; }
    public decimal? Price { get; init; }
    public DateTimeOffset? ListedDate { get; init; }
    public DateTimeOffset? RemovedDate { get; init; }
    public int? DaysOnMarket { get; init; }
    public double? Distance { get; init; }
    public double? Correlation { get; init; }
    public string? Status { get; init; }
}
```

Both records are in the `Domain/Cma/Models/` namespace alongside `Comp`, `CmaAnalysis`, etc.
They are NOT exposed to API consumers — `RentCastCompSource` maps them to domain `Comp` models.

#### Step 1d: Verify build

```bash
dotnet build apps/api/RealEstateStar.Domain/RealEstateStar.Domain.csproj --no-restore
```

Expected: Build succeeded, 0 errors.

#### Commit

```
feat: add IRentCastClient, RentCastValuation/Comp domain models, CompSource.RentCast
```

---

### - [ ] Task 2: `RentCastDiagnostics` — Domain/Shared

**Touches:** Domain only. No dependencies on other Phase 1 tasks.

#### Step 2a: Create `RentCastDiagnostics`

**Create:** `apps/api/RealEstateStar.Domain/Shared/RentCastDiagnostics.cs`

```csharp
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RealEstateStar.Domain.Shared;

public static class RentCastDiagnostics
{
    public const string ServiceName = "RealEstateStar.RentCast";
    public static readonly ActivitySource ActivitySource = new(ServiceName, "1.0.0");
    private static readonly Meter Meter = new(ServiceName, "1.0.0");

    // Counters
    public static readonly Counter<long> CallsTotal =
        Meter.CreateCounter<long>("rentcast.calls_total",
            description: "Total RentCast API calls made");
    public static readonly Counter<long> CallsFailed =
        Meter.CreateCounter<long>("rentcast.calls_failed",
            description: "Failed RentCast API calls");

    // Histograms
    public static readonly Histogram<long> CompsReturned =
        Meter.CreateHistogram<long>("rentcast.comps_returned",
            description: "Number of comparables returned per RentCast call");
    public static readonly Histogram<double> CallDurationMs =
        Meter.CreateHistogram<double>("rentcast.call_duration_ms", unit: "ms",
            description: "RentCast API call duration in milliseconds");
}
```

Diagnostics live in `Domain/Shared/` (same as `CmaDiagnostics`, `LeadDiagnostics`, etc.) so that
`Workers.Cma` can emit metrics without taking a dependency on `Clients.RentCast`.

#### Step 2b: Verify build

```bash
dotnet build apps/api/RealEstateStar.Domain/RealEstateStar.Domain.csproj --no-restore
```

Expected: Build succeeded, 0 errors.

#### Commit

```
feat: add RentCastDiagnostics to Domain/Shared
```

---

### - [ ] Task 3: `Clients.RentCast` project — `RentCastOptions`, `RentCastClient`, tests

**Touches:** Creates new project. Depends on Task 1 (IRentCastClient, RentCastValuation) and
Task 2 (RentCastDiagnostics). Run after those are merged, or coordinate branching.

#### Step 3a: Create the project directory and csproj

**Create:** `apps/api/RealEstateStar.Clients.RentCast/RealEstateStar.Clients.RentCast.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="RealEstateStar.Clients.RentCast.Tests" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Http" Version="10.0.0" />
    <PackageReference Include="Microsoft.Extensions.Options" Version="10.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\RealEstateStar.Domain\RealEstateStar.Domain.csproj" />
  </ItemGroup>
</Project>
```

Constraint: `Domain` is the only project reference. Architecture tests will fail if any other
`RealEstateStar.*` reference appears here.

#### Step 3b: Create `RentCastOptions`

**Create:** `apps/api/RealEstateStar.Clients.RentCast/RentCastOptions.cs`

```csharp
namespace RealEstateStar.Clients.RentCast;

public class RentCastOptions
{
    public string ApiKey { get; init; } = "";
    public string BaseUrl { get; init; } = "https://api.rentcast.io/v1/avm/value";
    public int TimeoutSeconds { get; init; } = 30;
    public int MonthlyLimitWarningPercent { get; init; } = 80;
}
```

#### Step 3c: Create `RentCastClient`

**Create:** `apps/api/RealEstateStar.Clients.RentCast/RentCastClient.cs`

```csharp
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Shared;

namespace RealEstateStar.Clients.RentCast;

internal sealed class RentCastClient(
    IHttpClientFactory httpClientFactory,
    IOptions<RentCastOptions> options,
    ILogger<RentCastClient> logger) : IRentCastClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<RentCastValuation?> GetValuationAsync(string address, CancellationToken ct)
    {
        var opts = options.Value;
        var encoded = Uri.EscapeDataString(address);
        var url = $"{opts.BaseUrl}?address={encoded}";

        using var activity = RentCastDiagnostics.ActivitySource.StartActivity("rentcast.get_valuation");
        activity?.SetTag("rentcast.address_length", address.Length);

        var sw = Stopwatch.GetTimestamp();
        RentCastDiagnostics.CallsTotal.Add(1);

        try
        {
            var client = httpClientFactory.CreateClient("RentCast");

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-Api-Key", opts.ApiKey);

            var response = await client.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                logger.LogError(
                    "[RENTCAST-010] RentCast API returned {StatusCode} for address. Body: {Body}",
                    (int)response.StatusCode, body);
                RentCastDiagnostics.CallsFailed.Add(1);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var dto = JsonSerializer.Deserialize<RentCastApiResponse>(json, JsonOptions);

            if (dto is null)
            {
                logger.LogError("[RENTCAST-010] RentCast response deserialized to null");
                RentCastDiagnostics.CallsFailed.Add(1);
                return null;
            }

            var valuation = MapToValuation(dto);

            logger.LogInformation(
                "[RENTCAST-001] RentCast returned valuation. Price={Price:C}, Comps={CompCount}",
                valuation.Price, valuation.Comparables.Count);

            RentCastDiagnostics.CompsReturned.Record(valuation.Comparables.Count);

            return valuation;
        }
        catch (TaskCanceledException)
        {
            logger.LogWarning("[RENTCAST-010] RentCast request timed out or was cancelled");
            RentCastDiagnostics.CallsFailed.Add(1);
            return null;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "[RENTCAST-010] RentCast HTTP request failed");
            RentCastDiagnostics.CallsFailed.Add(1);
            return null;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "[RENTCAST-010] Failed to deserialize RentCast response");
            RentCastDiagnostics.CallsFailed.Add(1);
            return null;
        }
        finally
        {
            RentCastDiagnostics.CallDurationMs.Record(
                Stopwatch.GetElapsedTime(sw).TotalMilliseconds);
        }
    }

    private static RentCastValuation MapToValuation(RentCastApiResponse dto) =>
        new()
        {
            Price = dto.Price,
            PriceRangeLow = dto.PriceRangeLow,
            PriceRangeHigh = dto.PriceRangeHigh,
            Comparables = (dto.Comparables ?? [])
                .Select(c => new RentCastComp
                {
                    FormattedAddress = c.FormattedAddress,
                    PropertyType = c.PropertyType,
                    Bedrooms = c.Bedrooms,
                    Bathrooms = c.Bathrooms,
                    SquareFootage = c.SquareFootage,
                    Price = c.Price,
                    ListedDate = c.ListedDate,
                    RemovedDate = c.RemovedDate,
                    DaysOnMarket = c.DaysOnMarket,
                    Distance = c.Distance,
                    Correlation = c.Correlation,
                    Status = c.Status
                })
                .ToList()
                .AsReadOnly()
        };

    // Internal DTOs — mirror RentCast JSON shape, not exposed outside this file
    private record RentCastApiResponse(
        [property: JsonPropertyName("price")] decimal Price,
        [property: JsonPropertyName("priceRangeLow")] decimal PriceRangeLow,
        [property: JsonPropertyName("priceRangeHigh")] decimal PriceRangeHigh,
        [property: JsonPropertyName("comparables")] List<RentCastApiComp>? Comparables);

    private record RentCastApiComp(
        [property: JsonPropertyName("formattedAddress")] string FormattedAddress,
        [property: JsonPropertyName("propertyType")] string? PropertyType,
        [property: JsonPropertyName("bedrooms")] int? Bedrooms,
        [property: JsonPropertyName("bathrooms")] decimal? Bathrooms,
        [property: JsonPropertyName("squareFootage")] int? SquareFootage,
        [property: JsonPropertyName("price")] decimal? Price,
        [property: JsonPropertyName("listedDate")] DateTimeOffset? ListedDate,
        [property: JsonPropertyName("removedDate")] DateTimeOffset? RemovedDate,
        [property: JsonPropertyName("daysOnMarket")] int? DaysOnMarket,
        [property: JsonPropertyName("distance")] double? Distance,
        [property: JsonPropertyName("correlation")] double? Correlation,
        [property: JsonPropertyName("status")] string? Status);
}
```

Note: Class is `internal sealed` — exposed via `InternalsVisibleTo` for tests and registered in
DI from `Api/Program.cs` using the interface `IRentCastClient`.

#### Step 3d: Create the test project

**Create:** `apps/api/tests/RealEstateStar.Clients.RentCast.Tests/RealEstateStar.Clients.RentCast.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="FluentAssertions" Version="8.8.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="Moq" Version="4.20.72" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../RealEstateStar.Clients.RentCast/RealEstateStar.Clients.RentCast.csproj" />
    <ProjectReference Include="../RealEstateStar.TestUtilities/RealEstateStar.TestUtilities.csproj" />
  </ItemGroup>
</Project>
```

#### Step 3e: Write tests — `RentCastClientTests.cs`

**Create:** `apps/api/tests/RealEstateStar.Clients.RentCast.Tests/RentCastClientTests.cs`

```csharp
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RealEstateStar.TestUtilities;

namespace RealEstateStar.Clients.RentCast.Tests;

public class RentCastClientTests
{
    private static RentCastOptions DefaultOptions() => new()
    {
        ApiKey = "test-api-key",
        BaseUrl = "https://api.rentcast.io/v1/avm/value",
        TimeoutSeconds = 30,
        MonthlyLimitWarningPercent = 80
    };

    private static (RentCastClient client, MockHttpMessageHandler handler) BuildClient(
        RentCastOptions? opts = null)
    {
        var handler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("RentCast")).Returns(httpClient);

        var options = Options.Create(opts ?? DefaultOptions());
        var client = new RentCastClient(factory.Object, options,
            NullLogger<RentCastClient>.Instance);
        return (client, handler);
    }

    private static string ValidResponseJson(int compCount = 2) =>
        $$"""
        {
          "price": 450000,
          "priceRangeLow": 420000,
          "priceRangeHigh": 480000,
          "comparables": [
            {{string.Join(",\n    ", Enumerable.Range(0, compCount).Select(i => $$"""
            {
              "formattedAddress": "{{i}} Oak Ave, Freehold, NJ 07728",
              "propertyType": "Single Family",
              "bedrooms": 3,
              "bathrooms": 2.0,
              "squareFootage": 1800,
              "price": 430000,
              "listedDate": "2025-01-10T00:00:00Z",
              "removedDate": "2025-02-01T00:00:00Z",
              "daysOnMarket": 22,
              "distance": 0.4,
              "correlation": 0.92,
              "status": "Inactive"
            }
            """))}}
          ]
        }
        """;

    [Fact]
    public async Task GetValuationAsync_SuccessResponse_ReturnsValuation()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ValidResponseJson(2))
        };

        var result = await client.GetValuationAsync("123 Main St, Freehold, NJ 07728",
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.Price.Should().Be(450000m);
        result.PriceRangeLow.Should().Be(420000m);
        result.PriceRangeHigh.Should().Be(480000m);
        result.Comparables.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetValuationAsync_ApiError_ReturnsNull()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Internal Server Error")
        };

        var result = await client.GetValuationAsync("123 Main St, Freehold, NJ 07728",
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetValuationAsync_Unauthorized_ReturnsNull()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"message\": \"Invalid API key\"}")
        };

        var result = await client.GetValuationAsync("123 Main St, Freehold, NJ 07728",
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetValuationAsync_Timeout_ReturnsNull()
    {
        var (client, handler) = BuildClient();
        handler.ExceptionToThrow = new TaskCanceledException("Request timed out");

        var result = await client.GetValuationAsync("123 Main St, Freehold, NJ 07728",
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetValuationAsync_NetworkError_ReturnsNull()
    {
        var (client, handler) = BuildClient();
        handler.ExceptionToThrow = new HttpRequestException("Connection refused");

        var result = await client.GetValuationAsync("123 Main St, Freehold, NJ 07728",
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetValuationAsync_EmptyComparables_ReturnsValuationWithEmptyList()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "price": 450000,
                  "priceRangeLow": 420000,
                  "priceRangeHigh": 480000,
                  "comparables": []
                }
                """)
        };

        var result = await client.GetValuationAsync("123 Main St, Freehold, NJ 07728",
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.Comparables.Should().BeEmpty();
    }

    [Fact]
    public async Task GetValuationAsync_NullComparables_ReturnsValuationWithEmptyList()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "price": 450000,
                  "priceRangeLow": 420000,
                  "priceRangeHigh": 480000,
                  "comparables": null
                }
                """)
        };

        var result = await client.GetValuationAsync("123 Main St, Freehold, NJ 07728",
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.Comparables.Should().BeEmpty();
    }

    [Fact]
    public async Task GetValuationAsync_BuildsCorrectRequestUrl()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ValidResponseJson(0))
        };

        await client.GetValuationAsync("123 Main St, Freehold, NJ 07728", CancellationToken.None);

        handler.LastRequest.Should().NotBeNull();
        var url = handler.LastRequest!.RequestUri!.ToString();
        url.Should().Contain("address=");
        url.Should().Contain(Uri.EscapeDataString("123 Main St, Freehold, NJ 07728"));
    }

    [Fact]
    public async Task GetValuationAsync_SetsApiKeyHeader()
    {
        var opts = DefaultOptions() with { ApiKey = "my-secret-key" };
        var (client, handler) = BuildClient(opts);
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ValidResponseJson(0))
        };

        await client.GetValuationAsync("123 Main St, Freehold, NJ 07728", CancellationToken.None);

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Headers.TryGetValues("X-Api-Key", out var values).Should().BeTrue();
        values!.Single().Should().Be("my-secret-key");
    }

    [Fact]
    public async Task GetValuationAsync_MapsAllCompFields()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "price": 450000,
                  "priceRangeLow": 420000,
                  "priceRangeHigh": 480000,
                  "comparables": [
                    {
                      "formattedAddress": "456 Oak Ave, Freehold, NJ 07728",
                      "propertyType": "Single Family",
                      "bedrooms": 4,
                      "bathrooms": 2.5,
                      "squareFootage": 2100,
                      "price": 510000,
                      "listedDate": "2025-01-05T00:00:00Z",
                      "removedDate": "2025-01-20T00:00:00Z",
                      "daysOnMarket": 15,
                      "distance": 0.6,
                      "correlation": 0.88,
                      "status": "Inactive"
                    }
                  ]
                }
                """)
        };

        var result = await client.GetValuationAsync("123 Main St, Freehold, NJ 07728",
            CancellationToken.None);

        var comp = result!.Comparables.Single();
        comp.FormattedAddress.Should().Be("456 Oak Ave, Freehold, NJ 07728");
        comp.PropertyType.Should().Be("Single Family");
        comp.Bedrooms.Should().Be(4);
        comp.Bathrooms.Should().Be(2.5m);
        comp.SquareFootage.Should().Be(2100);
        comp.Price.Should().Be(510000m);
        comp.DaysOnMarket.Should().Be(15);
        comp.Distance.Should().BeApproximately(0.6, 0.001);
        comp.Status.Should().Be("Inactive");
    }

    [Fact]
    public async Task GetValuationAsync_InvalidJson_ReturnsNull()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not valid json {{{")
        };

        var result = await client.GetValuationAsync("123 Main St, Freehold, NJ 07728",
            CancellationToken.None);

        result.Should().BeNull();
    }
}
```

Note: `MockHttpMessageHandler` must expose a `LastRequest` property. If `TestUtilities` does not
have one already, add it:

**Modify:** `apps/api/tests/RealEstateStar.TestUtilities/MockHttpMessageHandler.cs`

Add `public HttpRequestMessage? LastRequest { get; private set; }` and set it in `SendAsync`.
Check existing implementation first — if it already captures the request, use the existing field
name.

#### Step 3f: Verify build and tests

```bash
dotnet build apps/api/RealEstateStar.Clients.RentCast/RealEstateStar.Clients.RentCast.csproj
dotnet test apps/api/tests/RealEstateStar.Clients.RentCast.Tests/RealEstateStar.Clients.RentCast.Tests.csproj --logger "console;verbosity=minimal"
```

Expected: All 11+ tests pass.

#### Commit

```
feat: add Clients.RentCast project — RentCastClient, RentCastOptions, and tests
```

---

### - [ ] Task 4: `RentCastCompSource` + tests

**Touches:** `Workers.Cma` only. Depends on Task 1 (IRentCastClient, RentCastValuation, CompSource.RentCast).

#### Step 4a: Write tests first (TDD — RED phase)

**Create:** `apps/api/tests/RealEstateStar.Workers.Cma.Tests/RentCastCompSourceTests.cs`

```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Workers.Cma;

namespace RealEstateStar.Workers.Cma.Tests;

public class RentCastCompSourceTests
{
    private static CompSearchRequest MakeRequest(
        string address = "123 Main St",
        string city = "Freehold",
        string state = "NJ",
        string zip = "07728",
        int? beds = 3,
        int? baths = 2,
        int? sqft = 1800) => new()
    {
        Address = address,
        City = city,
        State = state,
        Zip = zip,
        Beds = beds,
        Baths = baths,
        SqFt = sqft
    };

    private static RentCastComp MakeComp(
        string address = "456 Oak Ave, Freehold, NJ 07728",
        decimal price = 430_000m,
        int sqft = 1750,
        string? status = "Inactive",
        DateTimeOffset? listedDate = null,
        DateTimeOffset? removedDate = null,
        string? propertyType = "Single Family",
        int? bedrooms = 3,
        decimal? bathrooms = 2.0m,
        int? daysOnMarket = 22,
        double? distance = 0.4) => new()
    {
        FormattedAddress = address,
        Price = price,
        SquareFootage = sqft,
        Status = status,
        ListedDate = listedDate ?? new DateTimeOffset(2025, 1, 10, 0, 0, 0, TimeSpan.Zero),
        RemovedDate = removedDate ?? new DateTimeOffset(2025, 2, 1, 0, 0, 0, TimeSpan.Zero),
        PropertyType = propertyType,
        Bedrooms = bedrooms,
        Bathrooms = bathrooms,
        DaysOnMarket = daysOnMarket,
        Distance = distance
    };

    private static RentCastValuation MakeValuation(
        IReadOnlyList<RentCastComp>? comparables = null) => new()
    {
        Price = 450_000m,
        PriceRangeLow = 420_000m,
        PriceRangeHigh = 480_000m,
        Comparables = comparables ?? []
    };

    // ---------------------------------------------------------------------------
    // MapComps — pure static method, tested directly without mocking
    // ---------------------------------------------------------------------------

    [Fact]
    public void MapComps_ValidComps_MapsAllFields()
    {
        var request = MakeRequest();
        var comp = MakeComp(
            address: "456 Oak Ave, Freehold, NJ 07728",
            price: 430_000m,
            sqft: 1750,
            status: "Inactive",
            removedDate: new DateTimeOffset(2025, 2, 1, 0, 0, 0, TimeSpan.Zero),
            propertyType: "Single Family",
            bedrooms: 3,
            bathrooms: 2.0m,
            daysOnMarket: 22,
            distance: 0.4);
        var valuation = MakeValuation([comp]);

        var result = RentCastCompSource.MapComps(valuation.Comparables, request,
            NullLogger.Instance);

        result.Should().HaveCount(1);
        var mapped = result[0];
        mapped.Address.Should().Be("456 Oak Ave, Freehold, NJ 07728");
        mapped.SalePrice.Should().Be(430_000m);
        mapped.SaleDate.Should().Be(new DateOnly(2025, 2, 1));
        mapped.Beds.Should().Be(3);
        mapped.Baths.Should().Be(2);
        mapped.Sqft.Should().Be(1750);
        mapped.DaysOnMarket.Should().Be(22);
        mapped.DistanceMiles.Should().BeApproximately(0.4, 0.001);
        mapped.Source.Should().Be(CompSource.RentCast);
    }

    [Fact]
    public void MapComps_InactiveStatus_UsesRemovedDateAsSaleDate()
    {
        var removed = new DateTimeOffset(2025, 3, 15, 0, 0, 0, TimeSpan.Zero);
        var listed = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var comp = MakeComp(status: "Inactive", removedDate: removed, listedDate: listed);

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), NullLogger.Instance);

        result[0].SaleDate.Should().Be(new DateOnly(2025, 3, 15));
    }

    [Fact]
    public void MapComps_ActiveStatus_UsesListedDateAsSaleDate()
    {
        var listed = new DateTimeOffset(2025, 1, 10, 0, 0, 0, TimeSpan.Zero);
        var comp = MakeComp(status: "Active", listedDate: listed, removedDate: null);

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), NullLogger.Instance);

        result[0].SaleDate.Should().Be(new DateOnly(2025, 1, 10));
    }

    [Fact]
    public void MapComps_NoBothDates_SkipsComp()
    {
        var comp = MakeComp(status: "Unknown", listedDate: null, removedDate: null);

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), NullLogger.Instance);

        result.Should().BeEmpty();
    }

    [Fact]
    public void MapComps_ZeroPrice_SkipsComp()
    {
        var comp = MakeComp(price: 0m);

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), NullLogger.Instance);

        result.Should().BeEmpty();
    }

    [Fact]
    public void MapComps_NullPrice_SkipsComp()
    {
        var comp = MakeComp() with { Price = null };

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), NullLogger.Instance);

        result.Should().BeEmpty();
    }

    [Fact]
    public void MapComps_ZeroSquareFootage_SkipsComp()
    {
        var comp = MakeComp(sqft: 0);

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), NullLogger.Instance);

        result.Should().BeEmpty();
    }

    [Fact]
    public void MapComps_NullSquareFootage_SkipsComp()
    {
        var comp = MakeComp() with { SquareFootage = null };

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), NullLogger.Instance);

        result.Should().BeEmpty();
    }

    [Fact]
    public void MapComps_WhitespaceAddress_SkipsComp()
    {
        var comp = MakeComp(address: "   ");

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), NullLogger.Instance);

        result.Should().BeEmpty();
    }

    [Fact]
    public void MapComps_NullBathrooms_DefaultsToZero()
    {
        var comp = MakeComp() with { Bathrooms = null };

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), NullLogger.Instance);

        result.Should().HaveCount(1);
        result[0].Baths.Should().Be(0);
    }

    [Fact]
    public void MapComps_FractionalBathrooms_RoundsToInt()
    {
        var comp = MakeComp() with { Bathrooms = 2.5m };

        var result = RentCastCompSource.MapComps([comp], MakeRequest(), NullLogger.Instance);

        result.Should().HaveCount(1);
        result[0].Baths.Should().Be(3);
    }

    [Fact]
    public void MapComps_MultiFamilyPropertyType_FilteredWhenSubjectHasSqFt()
    {
        var request = MakeRequest(sqft: 1800); // non-null sqft signals single-family subject
        var comp = MakeComp(propertyType: "Multi Family");

        var result = RentCastCompSource.MapComps([comp], request, NullLogger.Instance);

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Apartment")]
    [InlineData("Condominium")]
    [InlineData("Townhouse")]
    public void MapComps_ExcludedPropertyTypes_FilteredWhenSubjectHasSqFt(string propertyType)
    {
        var request = MakeRequest(sqft: 1800);
        var comp = MakeComp(propertyType: propertyType);

        var result = RentCastCompSource.MapComps([comp], request, NullLogger.Instance);

        result.Should().BeEmpty();
    }

    [Fact]
    public void MapComps_NullPropertyType_NotFiltered()
    {
        var request = MakeRequest(sqft: 1800);
        var comp = MakeComp() with { PropertyType = null };

        var result = RentCastCompSource.MapComps([comp], request, NullLogger.Instance);

        result.Should().HaveCount(1);
    }

    [Fact]
    public void MapComps_EmptyList_ReturnsEmpty()
    {
        var result = RentCastCompSource.MapComps([], MakeRequest(), NullLogger.Instance);

        result.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------
    // FetchAsync — uses mock IRentCastClient
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task FetchAsync_ClientReturnsNull_ReturnsEmpty()
    {
        var mockClient = new Mock<IRentCastClient>();
        mockClient
            .Setup(c => c.GetValuationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RentCastValuation?)null);

        var source = new RentCastCompSource(mockClient.Object,
            NullLogger<RentCastCompSource>.Instance);

        var result = await source.FetchAsync(MakeRequest(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchAsync_ClientReturnsValuation_ReturnsMappedComps()
    {
        var comp = MakeComp();
        var valuation = MakeValuation([comp]);

        var mockClient = new Mock<IRentCastClient>();
        mockClient
            .Setup(c => c.GetValuationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(valuation);

        var source = new RentCastCompSource(mockClient.Object,
            NullLogger<RentCastCompSource>.Instance);

        var result = await source.FetchAsync(MakeRequest(), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Source.Should().Be(CompSource.RentCast);
    }

    [Fact]
    public async Task FetchAsync_BuildsCorrectFullAddress()
    {
        string? capturedAddress = null;
        var mockClient = new Mock<IRentCastClient>();
        mockClient
            .Setup(c => c.GetValuationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((addr, _) => capturedAddress = addr)
            .ReturnsAsync((RentCastValuation?)null);

        var source = new RentCastCompSource(mockClient.Object,
            NullLogger<RentCastCompSource>.Instance);

        var request = MakeRequest("123 Main St", "Freehold", "NJ", "07728");
        await source.FetchAsync(request, CancellationToken.None);

        capturedAddress.Should().Be("123 Main St, Freehold, NJ 07728");
    }
}
```

#### Step 4b: Create `RentCastCompSource` (GREEN phase)

**Create:** `apps/api/RealEstateStar.Workers.Cma/RentCastCompSource.cs`

```csharp
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Cma.Models;

namespace RealEstateStar.Workers.Cma;

public class RentCastCompSource(
    IRentCastClient rentCastClient,
    ILogger<RentCastCompSource> logger) : ICompSource
{
    private static readonly HashSet<string> ExcludedPropertyTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Multi Family",
            "Apartment",
            "Condominium",
            "Townhouse"
        };

    public string Name => "RentCast";

    public async Task<List<Comp>> FetchAsync(CompSearchRequest request, CancellationToken ct)
    {
        var fullAddress = $"{request.Address}, {request.City}, {request.State} {request.Zip}";
        logger.LogInformation("[COMP-001] Fetching comps from RentCast for address={Address}",
            fullAddress);

        var valuation = await rentCastClient.GetValuationAsync(fullAddress, ct);
        if (valuation is null)
        {
            logger.LogWarning("[COMP-004] RentCast returned null for address={Address}",
                fullAddress);
            return [];
        }

        var comps = MapComps(valuation.Comparables, request, logger);
        logger.LogInformation("[COMP-003] Mapped {Count} valid comps from RentCast", comps.Count);
        return comps;
    }

    internal static List<Comp> MapComps(
        IReadOnlyList<RentCastComp> comparables,
        CompSearchRequest request,
        ILogger logger)
    {
        var result = new List<Comp>();

        foreach (var rc in comparables)
        {
            // Skip if price invalid
            if (rc.Price is null or <= 0)
                continue;

            // Skip if square footage invalid
            if (rc.SquareFootage is null or <= 0)
                continue;

            // Skip if address blank
            if (string.IsNullOrWhiteSpace(rc.FormattedAddress))
                continue;

            // Resolve sale date: Inactive status → use RemovedDate, otherwise ListedDate
            DateTimeOffset? resolvedDate = rc.Status?.Equals("Inactive",
                StringComparison.OrdinalIgnoreCase) == true
                ? rc.RemovedDate
                : rc.ListedDate;

            if (resolvedDate is null)
                continue;

            var saleDate = DateOnly.FromDateTime(resolvedDate.Value.UtcDateTime);

            // Property type filter: when subject has SqFt (single-family indicator),
            // exclude known multi-unit property types. Null type is kept (permissive).
            if (request.SqFt.HasValue
                && rc.PropertyType is not null
                && ExcludedPropertyTypes.Contains(rc.PropertyType))
            {
                continue;
            }

            result.Add(new Comp
            {
                Address = rc.FormattedAddress,
                SalePrice = rc.Price.Value,
                SaleDate = saleDate,
                Beds = rc.Bedrooms ?? 0,
                Baths = (int)Math.Round(rc.Bathrooms ?? 0),
                Sqft = rc.SquareFootage.Value,
                DaysOnMarket = rc.DaysOnMarket,
                DistanceMiles = rc.Distance ?? 0.0,
                Source = CompSource.RentCast
            });
        }

        return result;
    }
}
```

#### Step 4c: Run tests (GREEN phase)

```bash
dotnet test apps/api/tests/RealEstateStar.Workers.Cma.Tests/RealEstateStar.Workers.Cma.Tests.csproj --logger "console;verbosity=minimal"
```

Expected: All `RentCastCompSourceTests` pass. `ScraperCompSourceTests` still pass (not deleted yet).

#### Commit

```
feat: add RentCastCompSource — maps RentCast API response to domain Comp models
```

---

## Phase 2: Wiring (all independent within this phase — run Tasks 5–10 in parallel)

Start Phase 2 only after all Phase 1 tasks are complete and merged.

---

### - [ ] Task 5: `Program.cs` — remove ScraperCompSource loop, add RentCast DI, add startup log

**Modify:** `apps/api/RealEstateStar.Api/Program.cs`

#### Step 5a: Add using for RentCast client namespace

At the top of `Program.cs`, add alongside existing client usings:

```csharp
using RealEstateStar.Clients.RentCast;
using RealEstateStar.Domain.Cma.Interfaces;
```

(Note: `IRentCastClient` is in `RealEstateStar.Domain.Cma.Interfaces`, already in the domain
namespace which may already be partially imported. `RentCastClient` is in
`RealEstateStar.Clients.RentCast`.)

#### Step 5b: Add RentCast resilience policy to `PollyPolicies.cs`

**Modify:** `apps/api/RealEstateStar.Api/Infrastructure/PollyPolicies.cs`

Add the following extension method at the end of the class, following the pattern of
`AddScraperApiResilience`:

```csharp
/// <summary>
/// RentCast API: 1x retry (5s, jitter) + circuit breaker (5 failures / 60s → 1 min break).
/// Log codes: [RENTCAST-030] retry, [RENTCAST-031] CB opened, [RENTCAST-032] CB closed.
/// </summary>
public static IHttpClientBuilder AddRentCastResilience(this IHttpClientBuilder builder, ILogger logger)
{
    builder.AddResilienceHandler("rentcast-api", pipeline =>
    {
        pipeline.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 1,
            BackoffType = DelayBackoffType.Constant,
            Delay = TimeSpan.FromSeconds(5),
            UseJitter = true,
            OnRetry = args =>
            {
                logger.LogWarning(
                    "[RENTCAST-030] RentCast retry {Attempt}/{MaxAttempts} after {DelayMs}ms. Status: {Status}. Error: {Error}",
                    args.AttemptNumber + 1, 1,
                    args.RetryDelay.TotalMilliseconds,
                    args.Outcome.Result?.StatusCode,
                    args.Outcome.Exception?.Message);
                return ValueTask.CompletedTask;
            }
        });

        pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            FailureRatio = 1.0,
            MinimumThroughput = 5,
            SamplingDuration = TimeSpan.FromSeconds(60),
            BreakDuration = TimeSpan.FromMinutes(1),
            OnOpened = args =>
            {
                logger.LogError(
                    "[RENTCAST-031] RentCast API circuit OPEN for {BreakDurationSec}s. Last error: {Error}",
                    args.BreakDuration.TotalSeconds,
                    args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString());
                return ValueTask.CompletedTask;
            },
            OnClosed = _ =>
            {
                logger.LogInformation("[RENTCAST-032] RentCast API circuit CLOSED — resuming normal traffic.");
                return ValueTask.CompletedTask;
            }
        });
    });

    return builder;
}
```

#### Step 5c: Remove the CMA source variable and loop

Find and delete the following block in `Program.cs` (currently around lines 395–432):

```csharp
// DELETE these lines:
var cmaSources = builder.Configuration.GetSection("Pipeline:Cma:Sources")
    .Get<Dictionary<string, string>>() ?? new();
```

And delete the entire `foreach` loop:

```csharp
// DELETE this entire block:
foreach (var (sourceName, urlPattern) in cmaSources)
{
    if (!Enum.TryParse<CompSource>(sourceName, ignoreCase: true, out var source))
    {
        Log.Warning("[STARTUP-060] Unknown comp source '{SourceName}' in config, skipping", sourceName);
        continue;
    }
    builder.Services.AddSingleton<ICompSource>(sp =>
        new ScraperCompSource(
            sp.GetRequiredService<IAnthropicClient>(),
            sp.GetRequiredService<IScraperClient>(),
            source, urlPattern,
            sp.GetRequiredService<ILogger<ScraperCompSource>>()));
}
```

#### Step 5d: Add RentCast DI and startup log

Replace the deleted block with (immediately before `builder.Services.AddSingleton<ICompAggregator>`):

```csharp
// RentCast client — structured comp data for CMA pipeline
builder.Services.Configure<RentCastOptions>(builder.Configuration.GetSection("RentCast"));
builder.Services.AddSingleton<IRentCastClient, RentCastClient>();
builder.Services.AddHttpClient("RentCast")
    .AddRentCastResilience(pollyLogger);

// CMA comp source — single RentCast source replaces three-scraper loop
builder.Services.AddSingleton<ICompSource, RentCastCompSource>();

var rentCastKey = builder.Configuration["RentCast:ApiKey"];
if (!string.IsNullOrWhiteSpace(rentCastKey))
    Log.Information("[STARTUP-090] RentCast API configured. Monthly limit warning threshold: {Percent}%",
        builder.Configuration.GetValue<int>("RentCast:MonthlyLimitWarningPercent", 80));
else
    Log.Warning("[STARTUP-091] RentCast:ApiKey not configured — CMA comp fetch will return empty results");
```

#### Step 5e: Verify build

```bash
dotnet build apps/api/RealEstateStar.Api/RealEstateStar.Api.csproj --no-restore
```

Expected: Build succeeded, 0 errors, 0 warnings about missing `cmaSources`.

#### Commit

```
feat: wire RentCast DI in Program.cs — remove ScraperCompSource loop, add startup logs
```

---

### - [ ] Task 6: `OpenTelemetryExtensions.cs` — register RentCast diagnostics

**Modify:** `apps/api/RealEstateStar.Api/Diagnostics/OpenTelemetryExtensions.cs`

#### Step 6a: Add `RentCastDiagnostics` using

The file already imports `RealEstateStar.Domain.Shared` (for `LeadDiagnostics`, etc.). No new
using needed if `RentCastDiagnostics` is in the same namespace, which it is (`Domain.Shared`).

Verify the existing usings include `RealEstateStar.Domain.Shared` — it currently uses
`RealEstateStar.Clients.Scraper` for `ScraperDiagnostics`. `RentCastDiagnostics` lives in
`Domain.Shared`, so no new using statement is required.

#### Step 6b: Add to tracing sources

In the `.WithTracing(tracing => tracing` block, add after `.AddSource(GDriveDiagnostics.ServiceName)`:

```csharp
.AddSource(RentCastDiagnostics.ServiceName)
```

#### Step 6c: Add to metrics meters

In the `.WithMetrics(metrics => metrics` block, add after `.AddMeter(GDriveDiagnostics.ServiceName)`:

```csharp
.AddMeter(RentCastDiagnostics.ServiceName)
```

#### Step 6d: Verify build

```bash
dotnet build apps/api/RealEstateStar.Api/RealEstateStar.Api.csproj --no-restore
```

Expected: Build succeeded, 0 errors.

#### Commit

```
feat: register RentCastDiagnostics in OpenTelemetryExtensions
```

---

### - [ ] Task 7: `RentCastHealthCheck`

**Create:** `apps/api/RealEstateStar.Api/Health/RentCastHealthCheck.cs`

```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RealEstateStar.Api.Health;

public class RentCastHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct)
    {
        var apiKey = configuration["RentCast:ApiKey"];
        return string.IsNullOrWhiteSpace(apiKey)
            ? Task.FromResult(HealthCheckResult.Degraded("RentCast API key not configured"))
            : Task.FromResult(HealthCheckResult.Healthy("RentCast API key configured"));
    }
}
```

Then register the health check in `Program.cs`. Find the existing `AddHealthChecks()` call and
add alongside existing checks (e.g., after `ScraperApiHealthCheck`):

```csharp
.AddCheck<RentCastHealthCheck>("rentcast_api", tags: ["ready"])
```

#### Verify build

```bash
dotnet build apps/api/RealEstateStar.Api/RealEstateStar.Api.csproj --no-restore
```

Expected: Build succeeded, 0 errors.

#### Commit

```
feat: add RentCastHealthCheck — reports degraded when API key not configured
```

---

### - [ ] Task 8: `appsettings.json` — remove `Pipeline:Cma:Sources`, add `RentCast` block

**Modify:** `apps/api/RealEstateStar.Api/appsettings.json`

#### Step 8a: Remove `Pipeline:Cma:Sources`

Remove the `"Sources"` sub-object from `"Cma"`. Before:

```json
"Cma": {
  "Retry": { "MaxRetries": 3, "BaseDelaySeconds": 30, "MaxDelaySeconds": 600, "BackoffMultiplier": 2.0 },
  "Sources": {
    "zillow": "https://www.zillow.com/homedetails/{slug}",
    "redfin": "https://www.redfin.com/home/{slug}",
    "realtorcom": "https://www.realtor.com/realestateandhomes-detail/{slug}"
  }
}
```

After:

```json
"Cma": {
  "Retry": { "MaxRetries": 3, "BaseDelaySeconds": 30, "MaxDelaySeconds": 600, "BackoffMultiplier": 2.0 }
}
```

#### Step 8b: Add `RentCast` block

Add at the top level of the JSON, alongside `"Scraper"`:

```json
"RentCast": {
  "ApiKey": "",
  "MonthlyLimitWarningPercent": 80
}
```

The final `appsettings.json` should have both `"Scraper"` (unchanged — still used by Lead
Enrichment, Home Search, Profile Scraping) and the new `"RentCast"` block.

`BaseUrl` and `TimeoutSeconds` are intentionally absent from `appsettings.json` — they have
defaults in `RentCastOptions` and are override candidates via environment variables only if needed.

#### Step 8c: Verify the application builds and starts

```bash
dotnet build apps/api/RealEstateStar.Api/RealEstateStar.Api.csproj --no-restore
```

Expected: Build succeeded, 0 errors.

#### Commit

```
chore: update appsettings.json — remove Pipeline:Cma:Sources, add RentCast block
```

---

### - [ ] Task 9: Architecture tests — add `Clients.RentCast`, `IRentCastClient` DI test

#### Step 9a: Add `RealEstateStar.Clients.RentCast` to `DependencyTests.cs`

**Modify:** `apps/api/tests/RealEstateStar.Architecture.Tests/DependencyTests.cs`

Add a new `[InlineData]` entry in `Project_only_depends_on_allowed_projects`:

```csharp
[InlineData("RealEstateStar.Clients.RentCast", new[] { "Domain" })]
```

Place it alphabetically after `RealEstateStar.Clients.Gws`.

Also add `"RealEstateStar.Clients.RentCast"` to the `allowed` HashSet in `Api_only_depends_on_allowed_projects`:

```csharp
"RealEstateStar.Clients.RentCast",
```

#### Step 9b: Add `IRentCastClient` resolution to `DiRegistrationTests.cs`

**Modify:** `apps/api/tests/RealEstateStar.Architecture.Tests/DiRegistrationTests.cs`

Add at the top:

```csharp
using RealEstateStar.Domain.Cma.Interfaces;
```

Add a new `[InlineData]` entry in the `Domain_interface_resolves_from_DI` theory:

```csharp
[InlineData(typeof(IRentCastClient))]
```

#### Step 9c: Add project reference to `RealEstateStar.Architecture.Tests.csproj`

**Modify:** `apps/api/tests/RealEstateStar.Architecture.Tests/RealEstateStar.Architecture.Tests.csproj`

Add inside the existing `<ItemGroup>` with project references:

```xml
<ProjectReference Include="../../RealEstateStar.Clients.RentCast/RealEstateStar.Clients.RentCast.csproj" />
```

#### Step 9d: Run architecture tests

```bash
dotnet test apps/api/tests/RealEstateStar.Architecture.Tests/RealEstateStar.Architecture.Tests.csproj --logger "console;verbosity=minimal"
```

Expected: All tests pass, including the new `[InlineData("RealEstateStar.Clients.RentCast", ...)]`
assertion and `IRentCastClient` DI resolution.

#### Commit

```
test: add RealEstateStar.Clients.RentCast to architecture and DI registration tests
```

---

### - [ ] Task 10: Solution file and Dockerfile — add `Clients.RentCast`

#### Step 10a: Add projects to `RealEstateStar.Api.sln`

**Modify:** `apps/api/RealEstateStar.Api.sln`

Add the production project (generate a new GUID for it — use `dotnet sln add` or add manually):

```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "RealEstateStar.Clients.RentCast", "RealEstateStar.Clients.RentCast\RealEstateStar.Clients.RentCast.csproj", "{<NEW-GUID-1>}"
EndProject
```

Add the test project (nested under the `tests` solution folder):

```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "RealEstateStar.Clients.RentCast.Tests", "tests\RealEstateStar.Clients.RentCast.Tests\RealEstateStar.Clients.RentCast.Tests.csproj", "{<NEW-GUID-2>}"
EndProject
```

Then add the configuration entries for both projects in `GlobalSection(ProjectConfigurationPlatforms)`,
following the existing pattern (Debug/Release × Any CPU/x64/x86 = 6 lines per project).

The easiest method is:

```bash
cd apps/api
dotnet sln RealEstateStar.Api.sln add RealEstateStar.Clients.RentCast/RealEstateStar.Clients.RentCast.csproj
dotnet sln RealEstateStar.Api.sln add tests/RealEstateStar.Clients.RentCast.Tests/RealEstateStar.Clients.RentCast.Tests.csproj
```

#### Step 10b: Add project reference to `RealEstateStar.Api.csproj`

**Modify:** `apps/api/RealEstateStar.Api/RealEstateStar.Api.csproj`

Add inside the existing `<ItemGroup>` with `ProjectReference` entries:

```xml
<ProjectReference Include="..\RealEstateStar.Clients.RentCast\RealEstateStar.Clients.RentCast.csproj" />
```

Place it alphabetically after `Clients.GSheets`.

#### Step 10c: Add COPY line to Dockerfile

**Modify:** `apps/api/Dockerfile`

Add after the existing `COPY` for `Clients.Gws`:

```dockerfile
COPY ["RealEstateStar.Clients.RentCast/RealEstateStar.Clients.RentCast.csproj", "RealEstateStar.Clients.RentCast/"]
```

This line must appear BEFORE `RUN dotnet restore` so the restore layer caching works correctly.

#### Step 10d: Full solution build verification

```bash
dotnet build apps/api/RealEstateStar.Api.sln --no-restore --configuration Release
```

Expected: Build succeeded, 0 errors.

#### Commit

```
chore: add Clients.RentCast to solution file, Api.csproj project refs, and Dockerfile
```

---

## Phase 3: Dead Code Removal (run Tasks 11–12 in parallel after Phase 2 completes)

---

### - [ ] Task 11: Delete `ScraperCompSource.cs`

**Delete:** `apps/api/RealEstateStar.Workers.Cma/ScraperCompSource.cs`

This file is 188 lines. It depends on `IAnthropicClient` and `IScraperClient` for HTML scraping
and Claude extraction — both of which `RentCastCompSource` eliminates from the CMA path.

After deletion, verify the CMA project still builds:

```bash
dotnet build apps/api/RealEstateStar.Workers.Cma/RealEstateStar.Workers.Cma.csproj --no-restore
```

Expected: Build succeeded. `RentCastCompSource` is the only `ICompSource` implementation remaining
in the project.

#### Commit

```
chore: delete ScraperCompSource — replaced by RentCastCompSource
```

---

### - [ ] Task 12: Delete `ScraperCompSourceTests.cs`

**Delete:** `apps/api/tests/RealEstateStar.Workers.Cma.Tests/ScraperCompSourceTests.cs`

This file is 241 lines. All test scenarios it covered (URL slug building, HTML parsing, Claude
JSON parsing) are no longer relevant — the new pipeline makes no HTTP calls to scrapers and does
no Claude extraction in the comp fetch step.

After deletion, run the CMA test project:

```bash
dotnet test apps/api/tests/RealEstateStar.Workers.Cma.Tests/RealEstateStar.Workers.Cma.Tests.csproj --logger "console;verbosity=minimal"
```

Expected: All remaining tests pass (`ClaudeCmaAnalyzerTests`, `CmaPdfGeneratorTests`,
`CmaProcessingWorkerTests`, `RentCastCompSourceTests`). Zero test failures.

#### Commit

```
chore: delete ScraperCompSourceTests — tests deleted source file ScraperCompSource
```

---

## Phase 4: Documentation + Deploy (run Tasks 13–17 in parallel after Phase 3 completes)

---

### - [ ] Task 13: Update `CLAUDE.md` and `.claude/CLAUDE.md`

#### Step 13a: Update `CLAUDE.md` (root)

**Modify:** `c:\Users\Edward.Rosado\Real-Estate-Star\CLAUDE.md`

In the `## Infrastructure` table, the API row describes "21 isolated projects". Update to "22
isolated projects" to reflect the addition of `Clients.RentCast`.

#### Step 13b: Update `.claude/CLAUDE.md`

**Modify:** `c:\Users\Edward.Rosado\Real-Estate-Star\.claude\CLAUDE.md`

In the `apps/api/` section of the Monorepo Structure block, add:

```
    RealEstateStar.Clients.RentCast/     # RentCast property valuation API client
```

Place it alphabetically after `RealEstateStar.Clients.Gws/`.

Also update the test projects count in the same block:

```
    tests/                             # 24 test projects (1:1 with production + Architecture.Tests + TestUtilities)
```

(was 23, now 24 with `Clients.RentCast.Tests` added).

#### Commit

```
docs: update CLAUDE.md and .claude/CLAUDE.md — add Clients.RentCast to project list
```

---

### - [ ] Task 14: Update architecture docs

**Modify:** `docs/architecture/README.md` (if it exists; check first)

Add `Clients.RentCast` to any project listing or dependency diagrams that enumerate the
`Clients.*` projects. The key facts to capture:

- `Clients.RentCast` depends on `Domain` only
- `Clients.RentCast` implements `IRentCastClient` (defined in `Domain/Cma/Interfaces/`)
- `Workers.Cma` uses `IRentCastClient` via DI (injected by `Api/Program.cs`)
- CMA pipeline now uses one source (RentCast) instead of three scrapers

If `docs/architecture/README.md` does not exist, skip this task. The spec and plan docs are
sufficient.

#### Commit

```
docs: update architecture docs — add Clients.RentCast project listing
```

---

### - [ ] Task 15: Grafana dashboard — add RentCast panel group

The Grafana dashboard is configured at `https://grafana.real-estate-star.com` (not a file in the
repo). This task is a manual step performed in the Grafana UI.

**Panels to add** (follow pattern of existing ScraperAPI panel group):

| Panel name | PromQL / metric | Type |
|------------|----------------|------|
| RentCast calls/min | `rate(rentcast_calls_total[5m])` | Time series |
| RentCast failures/min | `rate(rentcast_calls_failed[5m])` | Time series |
| Comps returned (p50/p95) | `histogram_quantile(0.95, sum(rate(rentcast_comps_returned_bucket[5m])) by (le))` | Time series |
| API call duration (p50/p95) | `histogram_quantile(0.95, sum(rate(rentcast_call_duration_ms_bucket[5m])) by (le))` | Time series |

Place the RentCast panel group adjacent to the existing ScraperAPI panel group in the pipeline
observability section.

#### Document completion

When done, note in the PR description: "Grafana panels added manually — see dashboard
https://grafana.real-estate-star.com".

---

### - [ ] Task 16: `deploy-api.yml` — add `RENTCAST_API_KEY` secret injection

**Modify:** `.github/workflows/deploy-api.yml`

#### Step 16a: Add secret to `Update Container App secrets` step

In the `env:` block of the "Update Container App secrets" step, add:

```yaml
RENTCAST_KEY: ${{ secrets.RENTCAST_API_KEY }}
```

In the `run:` block's `SECRETS=()` array, add:

```bash
[ -n "$RENTCAST_KEY" ]   && SECRETS+=("rentcast-api-key=$RENTCAST_KEY")
```

#### Step 16b: Add env var to `Deploy to Container App` step

In the `env:` block of the "Deploy to Container App" step, add:

```yaml
RENTCAST_KEY: ${{ secrets.RENTCAST_API_KEY }}
```

In the `run:` block, add the conditional env var injection:

```bash
if [ -n "$RENTCAST_KEY" ]; then
  EXTRA_ENV_VARS="$EXTRA_ENV_VARS RentCast__ApiKey=secretref:rentcast-api-key"
fi
```

#### Step 16c: Add `RENTCAST_API_KEY` to GitHub repository secrets

Go to `https://github.com/edward-rosado/Real-Estate-Star/settings/secrets/actions` and add:

- Name: `RENTCAST_API_KEY`
- Value: (the RentCast API key from the RentCast dashboard)

This is a manual step — note it in the PR description.

#### Commit

```
ci: add RENTCAST_API_KEY secret injection to deploy-api.yml
```

---

### - [ ] Task 17: Full build, test, and coverage verification

This is the final gate task that validates all phases are complete and correct.

#### Step 17a: Full solution build

```bash
dotnet build apps/api/RealEstateStar.Api.sln --configuration Release
```

Expected: Build succeeded, 0 errors, 0 warnings about missing references.

#### Step 17b: Full test run

```bash
dotnet test apps/api/RealEstateStar.Api.sln --no-build --configuration Release --logger "console;verbosity=minimal"
```

Expected: All tests pass. Zero failures.

Verify new test counts:
- `RealEstateStar.Clients.RentCast.Tests`: 11+ tests (RentCastClientTests)
- `RealEstateStar.Workers.Cma.Tests`: new `RentCastCompSourceTests` (18+ tests), `ScraperCompSourceTests` deleted
- `RealEstateStar.Architecture.Tests`: new `IRentCastClient` DI test, new `Clients.RentCast` dependency test

#### Step 17c: Branch coverage verification

```bash
bash apps/api/scripts/coverage.sh --low-only
```

Expected: No classes below 100% branch coverage in:
- `RentCastClient` — all error paths covered
- `RentCastCompSource` — all filter branches covered
- `RentCastHealthCheck` — both key-present and key-absent paths covered

If any class is below 100%, add tests before merging.

#### Step 17d: Architecture test specific run

```bash
dotnet test apps/api/tests/RealEstateStar.Architecture.Tests --no-build --configuration Release --logger "console;verbosity=minimal"
```

Expected: All 41+ architecture tests pass (31 reflection-based + 10 NetArchTest + new ones added
in Task 9).

#### Step 17e: Verify `ScraperCompSource` is truly gone

```bash
grep -r "ScraperCompSource" apps/api/ --include="*.cs"
```

Expected: Zero matches. The class and all references to it are deleted.

#### Step 17f: Smoke check for dead code patterns

```bash
grep -r "Pipeline:Cma:Sources" apps/api/ --include="*.json" --include="*.cs"
```

Expected: Zero matches. Config key removed from `appsettings.json` and no remaining code reads it.

#### Commit

```
test: full coverage verification — all branches covered, architecture tests green
```

---

## Verification Checklist

Before marking the feature complete:

- [ ] `dotnet build apps/api/RealEstateStar.Api.sln --configuration Release` — 0 errors
- [ ] `dotnet test apps/api/RealEstateStar.Api.sln --configuration Release` — 0 failures
- [ ] `bash apps/api/scripts/coverage.sh --low-only` — no classes below 100% in new code
- [ ] `grep -r "ScraperCompSource" apps/api/ --include="*.cs"` — 0 matches
- [ ] `grep -r "Pipeline:Cma:Sources" apps/api/` — 0 matches
- [ ] Architecture test `Clients.RentCast` `[InlineData]` passes
- [ ] DI test `IRentCastClient` resolves from container
- [ ] `RentCastHealthCheck` registered in health checks
- [ ] `appsettings.json` has `"RentCast"` block, no `"Cma:Sources"` block
- [ ] `Dockerfile` has `COPY` line for `RealEstateStar.Clients.RentCast.csproj`
- [ ] `RealEstateStar.Api.sln` includes both `Clients.RentCast` and `Clients.RentCast.Tests`
- [ ] `deploy-api.yml` injects `RENTCAST_API_KEY`
- [ ] GitHub secret `RENTCAST_API_KEY` added (manual step — confirm in PR description)
- [ ] Grafana panels added (manual step — confirm in PR description)
- [ ] `CLAUDE.md` updated: 22 projects, `Clients.RentCast` in table
- [ ] `.claude/CLAUDE.md` updated: 24 test projects, `Clients.RentCast` in project list

---

## File Reference Map

| File | Action | Phase | Task |
|------|--------|-------|------|
| `Domain/Cma/Models/Comp.cs` | Modify — add `RentCast` enum value | 1 | 1 |
| `Domain/Cma/Interfaces/IRentCastClient.cs` | Create | 1 | 1 |
| `Domain/Cma/Models/RentCastValuation.cs` | Create | 1 | 1 |
| `Domain/Shared/RentCastDiagnostics.cs` | Create | 1 | 2 |
| `Clients.RentCast/*.csproj` | Create | 1 | 3 |
| `Clients.RentCast/RentCastOptions.cs` | Create | 1 | 3 |
| `Clients.RentCast/RentCastClient.cs` | Create | 1 | 3 |
| `tests/RentCast.Tests/*.csproj` | Create | 1 | 3 |
| `tests/RentCast.Tests/RentCastClientTests.cs` | Create | 1 | 3 |
| `Workers.Cma/RentCastCompSource.cs` | Create | 1 | 4 |
| `tests/Workers.Cma.Tests/RentCastCompSourceTests.cs` | Create | 1 | 4 |
| `Api/Program.cs` | Modify — DI wiring, remove loop, add startup log | 2 | 5 |
| `Api/Infrastructure/PollyPolicies.cs` | Modify — add `AddRentCastResilience` | 2 | 5 |
| `Api/Diagnostics/OpenTelemetryExtensions.cs` | Modify — register RentCast sources/meters | 2 | 6 |
| `Api/Health/RentCastHealthCheck.cs` | Create | 2 | 7 |
| `Api/appsettings.json` | Modify — remove Sources block, add RentCast block | 2 | 8 |
| `Architecture.Tests/DependencyTests.cs` | Modify — add `[InlineData]` for RentCast | 2 | 9 |
| `Architecture.Tests/DiRegistrationTests.cs` | Modify — add `IRentCastClient` | 2 | 9 |
| `Architecture.Tests/*.csproj` | Modify — add project reference | 2 | 9 |
| `RealEstateStar.Api.sln` | Modify — add two projects | 2 | 10 |
| `RealEstateStar.Api.csproj` | Modify — add project reference | 2 | 10 |
| `Dockerfile` | Modify — add COPY line | 2 | 10 |
| `Workers.Cma/ScraperCompSource.cs` | Delete | 3 | 11 |
| `tests/Workers.Cma.Tests/ScraperCompSourceTests.cs` | Delete | 3 | 12 |
| `CLAUDE.md` | Modify — project count and table | 4 | 13 |
| `.claude/CLAUDE.md` | Modify — project list and test count | 4 | 13 |
| `docs/architecture/README.md` | Modify (if exists) | 4 | 14 |
| Grafana dashboard | Manual — add RentCast panels | 4 | 15 |
| `.github/workflows/deploy-api.yml` | Modify — secret injection | 4 | 16 |
| GitHub secrets | Manual — add `RENTCAST_API_KEY` | 4 | 16 |
