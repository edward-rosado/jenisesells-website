# REPR Vertical Slice Migration Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Refactor `apps/api` from a flat `Models/` layered structure to true REPR vertical slices where every file serving one HTTP operation lives in `Features/{Feature}/{Operation}/`. Establish `IEndpoint` auto-registration so `Program.cs` never needs manual route entries again.

**Architecture:** .NET Minimal API. Each endpoint is a self-contained folder containing exactly `{Operation}Request.cs`, `{Operation}Endpoint.cs`, and `{Operation}Response.cs`. Shared domain models live at the feature level (`Features/{Feature}/`) or in `Common/` if cross-feature. Auto-registration via `EndpointExtensions.MapEndpoints()` replaces all inline `app.MapGet/MapPost` calls.

**Design Doc:** `docs/plans/2026-03-09-repr-migration-plan.md` (this file)

**Tech Stack:** .NET 8 Minimal API, C#, xUnit, WebApplicationFactory

---

## Current State

```
apps/api/src/RealEstateStar.Api/
├── Models/                        ← ❌ Flat, cross-feature — to be deleted
│   ├── AgentConfig.cs             ← Domain model, shared across features
│   ├── CmaRequest.cs              ← Orphan — no endpoint uses it yet
│   └── CmaResult.cs               ← Orphan — no endpoint uses it yet
├── Program.cs                     ← ❌ Inline routes, needs slim-down
└── RealEstateStar.Api.csproj
```

## Target State

```
apps/api/src/RealEstateStar.Api/
├── Common/
│   └── AgentConfig.cs             ← namespace RealEstateStar.Api.Common
├── Endpoints/
│   ├── IEndpoint.cs               ← Contract all endpoints implement
│   └── EndpointExtensions.cs      ← Auto-discovery + registration
├── Features/
│   └── Cma/
│       └── Submit/
│           ├── SubmitCmaRequest.cs    ← namespace RealEstateStar.Api.Features.Cma.Submit
│           ├── SubmitCmaEndpoint.cs   ← namespace RealEstateStar.Api.Features.Cma.Submit
│           └── SubmitCmaResponse.cs   ← namespace RealEstateStar.Api.Features.Cma.Submit
└── Program.cs                     ← Slim: AddEndpoints() + MapEndpoints() only
```

---

## Task 1: Add IEndpoint Interface and Auto-Registration

**Files to create:**
- Create: `apps/api/src/RealEstateStar.Api/Endpoints/IEndpoint.cs`
- Create: `apps/api/src/RealEstateStar.Api/Endpoints/EndpointExtensions.cs`

**Step 1: Create IEndpoint**

```csharp
// apps/api/src/RealEstateStar.Api/Endpoints/IEndpoint.cs
namespace RealEstateStar.Api.Endpoints;

public interface IEndpoint
{
    void MapEndpoint(WebApplication app);
}
```

**Step 2: Create EndpointExtensions**

```csharp
// apps/api/src/RealEstateStar.Api/Endpoints/EndpointExtensions.cs
using System.Reflection;

namespace RealEstateStar.Api.Endpoints;

public static class EndpointExtensions
{
    /// <summary>
    /// Scans the assembly for all IEndpoint implementations and registers
    /// them as transient services so MapEndpoints() can resolve them.
    /// Call once in Program.cs: builder.Services.AddEndpoints(Assembly.GetExecutingAssembly())
    /// </summary>
    public static IServiceCollection AddEndpoints(
        this IServiceCollection services,
        Assembly assembly)
    {
        var endpointTypes = assembly.GetExportedTypes()
            .Where(t => typeof(IEndpoint).IsAssignableFrom(t)
                     && t is { IsAbstract: false, IsInterface: false });

        foreach (var type in endpointTypes)
            services.AddTransient(typeof(IEndpoint), type);

        return services;
    }

    /// <summary>
    /// Resolves all registered IEndpoint instances and calls MapEndpoint on each.
    /// Call once in Program.cs: app.MapEndpoints()
    /// New endpoints become live the moment their class is created — no manual registration needed.
    /// </summary>
    public static WebApplication MapEndpoints(this WebApplication app)
    {
        var endpoints = app.Services.GetRequiredService<IEnumerable<IEndpoint>>();

        foreach (var endpoint in endpoints)
            endpoint.MapEndpoint(app);

        return app;
    }
}
```

**Verify:** Build passes. No tests change yet.

---

## Task 2: Create Features/ Folder Skeleton

**Folders to create (empty — structure checkpoint for PR review):**

```bash
mkdir -p apps/api/src/RealEstateStar.Api/Common
mkdir -p apps/api/src/RealEstateStar.Api/Features/Cma/Submit
mkdir -p apps/api/src/RealEstateStar.Api/Features/Cma/GetStatus
```

Add a `.gitkeep` to `GetStatus/` since it has no files yet:

```bash
touch apps/api/src/RealEstateStar.Api/Features/Cma/GetStatus/.gitkeep
```

**Verify:** Build passes. Folder structure visible in git diff.

---

## Task 3: Migrate CmaRequest → SubmitCmaRequest

**Files:**
- Create: `apps/api/src/RealEstateStar.Api/Features/Cma/Submit/SubmitCmaRequest.cs`
- Delete: `apps/api/src/RealEstateStar.Api/Models/CmaRequest.cs`

```csharp
// apps/api/src/RealEstateStar.Api/Features/Cma/Submit/SubmitCmaRequest.cs
using System.Text.Json.Serialization;

namespace RealEstateStar.Api.Features.Cma.Submit;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SellTimeline
{
    Asap,
    OneToThreeMonths,
    ThreeToSixMonths,
    SixToTwelveMonths,
    Curious
}

/// <summary>
/// Mirrors the FormData fields from CmaForm.tsx.
/// Received as application/x-www-form-urlencoded from the agent site.
/// </summary>
public sealed record SubmitCmaRequest
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Email { get; init; }
    public required string Phone { get; init; }
    public required string Address { get; init; }
    public required string City { get; init; }
    public required string State { get; init; }
    public required string Zip { get; init; }
    public required string Timeline { get; init; }
    public string? Notes { get; init; }
}
```

**Verify:** `grep -r "CmaRequest" apps/api/src/` returns zero hits. Build passes.

---

## Task 4: Migrate CmaResult → SubmitCmaResponse

**Files:**
- Create: `apps/api/src/RealEstateStar.Api/Features/Cma/Submit/SubmitCmaResponse.cs`
- Delete: `apps/api/src/RealEstateStar.Api/Models/CmaResult.cs`

```csharp
// apps/api/src/RealEstateStar.Api/Features/Cma/Submit/SubmitCmaResponse.cs
namespace RealEstateStar.Api.Features.Cma.Submit;

/// <summary>
/// Returned from POST /cma after job submission.
/// The client polls GET /cma/{jobId} for async status updates.
/// </summary>
public sealed record SubmitCmaResponse
{
    public required string JobId { get; init; }
    public required string Status { get; init; }
}
```

**Verify:** `grep -r "CmaResult" apps/api/src/` returns zero hits. Build passes.

---

## Task 5: Migrate AgentConfig to Common/

**Files:**
- Create: `apps/api/src/RealEstateStar.Api/Common/AgentConfig.cs`
- Delete: `apps/api/src/RealEstateStar.Api/Models/AgentConfig.cs`
- Delete: `apps/api/src/RealEstateStar.Api/Models/` (now empty)

Change only the namespace declaration at the top of the file:

```csharp
// Old first line:
namespace RealEstateStar.Api.Models;

// New first line:
namespace RealEstateStar.Api.Common;
```

All type names (`AgentConfig`, `AgentIdentity`, etc.) stay unchanged.

**Verify:**
```bash
grep -r "RealEstateStar.Api.Models" apps/api/
```
Returns zero hits. `Models/` directory is gone. Build passes.

---

## Task 6: Implement SubmitCmaEndpoint

**Files:**
- Create: `apps/api/src/RealEstateStar.Api/Features/Cma/Submit/SubmitCmaEndpoint.cs`

Add `Microsoft.Extensions.Caching.Memory` — already available via the framework, no new NuGet needed.

```csharp
// apps/api/src/RealEstateStar.Api/Features/Cma/Submit/SubmitCmaEndpoint.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using RealEstateStar.Api.Endpoints;

namespace RealEstateStar.Api.Features.Cma.Submit;

public sealed class SubmitCmaEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app) =>
        app.MapPost("/cma", Handle)
            .WithName("SubmitCma")
            .WithSummary("Submit a CMA request from an agent site lead form.")
            .WithTags("CMA")
            .Accepts<SubmitCmaRequest>("application/x-www-form-urlencoded")
            .Produces<SubmitCmaResponse>(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status400BadRequest);

    // internal (not private) so tests can call Handle directly without reflection.
    // Dependencies are resolved by Minimal API DI at call time — no constructor needed.
    internal static async Task<IResult> Handle(
        [FromForm] SubmitCmaRequest request,
        IMemoryCache cache,
        ILogger<SubmitCmaEndpoint> logger,
        CancellationToken ct)
    {
        var jobId = Guid.NewGuid().ToString("N");

        cache.Set(jobId, new { Status = "queued", Request = request },
            TimeSpan.FromHours(24));

        logger.LogInformation(
            "CMA job {JobId} queued for {Email} at {City}, {State}",
            jobId, request.Email, request.City, request.State);

        return Results.Accepted(
            $"/cma/{jobId}",
            new SubmitCmaResponse { JobId = jobId, Status = "queued" });
    }
}
```

**Verify:** Build passes. Endpoint not yet live (not wired in Program.cs).

---

## Task 7: Slim Program.cs and Wire Auto-Registration

**Files:**
- Modify: `apps/api/src/RealEstateStar.Api/Program.cs`

Replace the full contents with:

```csharp
using System.Reflection;
using RealEstateStar.Api.Endpoints;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Structured logging
builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration)
          .Enrich.WithProperty("Application", "RealEstateStar.Api")
          .WriteTo.Console());

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Health checks
builder.Services.AddHealthChecks();

// Memory cache — used by CMA job store
builder.Services.AddMemoryCache();

// CORS — allow agent site subdomains
builder.Services.AddCors(options =>
    options.AddPolicy("AgentSites", policy =>
        policy.SetIsOriginAllowed(origin =>
            new Uri(origin).Host is "localhost"
            or var h when h.EndsWith(".realestatestar.com"))
              .AllowAnyHeader()
              .AllowAnyMethod()));

// Auto-discover and register every IEndpoint in this assembly.
// New endpoints are live the moment their class is created — no manual registration here.
builder.Services.AddEndpoints(Assembly.GetExecutingAssembly());

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseCors("AgentSites");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Split health checks: /health/live = liveness, /health/ready = readiness
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

// All endpoint routes registered here via IEndpoint.MapEndpoint(app).
// Do not add app.MapGet / app.MapPost calls below this line.
app.MapEndpoints();

app.Run();

// Make Program accessible for WebApplicationFactory in integration tests.
public partial class Program { }
```

**Verify:** `dotnet build` passes. `dotnet run` starts. Swagger shows `POST /cma`.

---

## Task 8: Update Tests

**Files:**
- Modify: `apps/api/tests/RealEstateStar.Api.Tests/HealthCheckTests.cs`
- Create: `apps/api/tests/RealEstateStar.Api.Tests/Features/Cma/SubmitCmaEndpointTests.cs`

**Step 1: Update health check path**

Change `/healthz` → `/health/live` in `HealthCheckTests.cs`:

```csharp
// Before
var response = await _client.GetAsync("/healthz");

// After
var response = await _client.GetAsync("/health/live");
```

**Step 2: Add SubmitCma integration test**

```csharp
// apps/api/tests/RealEstateStar.Api.Tests/Features/Cma/SubmitCmaEndpointTests.cs
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Cma;

public class SubmitCmaEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SubmitCmaEndpointTests(WebApplicationFactory<Program> factory)
        => _client = factory.CreateClient();

    [Fact]
    public async Task SubmitCma_ValidForm_Returns202WithJobId()
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FirstName"] = "Jane",
            ["LastName"]  = "Smith",
            ["Email"]     = "jane@example.com",
            ["Phone"]     = "555-0100",
            ["Address"]   = "123 Main St",
            ["City"]      = "Montclair",
            ["State"]     = "NJ",
            ["Zip"]       = "07042",
            ["Timeline"]  = "Asap"
        });

        var response = await _client.PostAsync("/cma", form);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("jobId", body);
        Assert.Contains("queued", body);
    }

    [Fact]
    public async Task SubmitCma_MissingRequiredField_Returns400()
    {
        // Missing Email — should fail model binding
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FirstName"] = "Jane",
            ["LastName"]  = "Smith"
            // Email omitted intentionally
        });

        var response = await _client.PostAsync("/cma", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
```

**Step 3: Run coverage**

```bash
dotnet test apps/api/ --collect:"XPlat Code Coverage"
```

100% branch coverage is required before this task is complete.

**Verify:** All tests green. Coverage report shows `SubmitCmaEndpoint.Handle` fully covered.

---

## Git Commit Plan

Execute one commit per task. Each commit must build and all tests must pass before moving to the next.

```
commit 1: chore: add IEndpoint interface and MapEndpoints auto-registration
commit 2: refactor: create Features/ vertical slice folder skeleton
commit 3: refactor: migrate CmaRequest to Features/Cma/Submit as SubmitCmaRequest
commit 4: refactor: migrate CmaResult to Features/Cma/Submit as SubmitCmaResponse
commit 5: refactor: migrate AgentConfig to Common/ and remove Models/ folder
commit 6: feat: implement SubmitCmaEndpoint — first REPR vertical slice
commit 7: refactor: wire IEndpoint auto-registration and slim Program.cs
commit 8: test: update health path and add SubmitCma integration tests
```

---

## Rules for All Future Endpoints

1. Create `Features/{Feature}/{Operation}/` — never add to an existing Models/ folder
2. Name types with the operation prefix: `{Operation}Request`, `{Operation}Endpoint`, `{Operation}Response`
3. Implement `IEndpoint` — the endpoint goes live automatically via `MapEndpoints()`
4. Make `Handle` `internal static` — dependencies via Minimal API DI, no constructor
5. If no response body (SSE/streaming), omit the Response record and add a comment explaining why
6. Shared types used by >1 endpoint in same feature → `Features/{Feature}/{Feature}Types.cs`
7. Shared types used by >1 feature → `Common/`
8. Write the failing test before the implementation (TDD — project rule)
9. Run coverage after every task — 100% branch coverage is required
