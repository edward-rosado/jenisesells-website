# CMA Pipeline Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a hybrid AI + deterministic pipeline that generates Comparative Market Analyses from lead form submissions, delivering branded PDFs via email in under 2 minutes.

**Architecture:** .NET 10 API orchestrates the pipeline — fetching comps from multiple sources in parallel, researching leads from public data, calling Claude API for analysis, generating PDFs with QuestPDF, and using Google Workspace CLI (`gws`) for Drive organization, Lead Brief creation, email delivery, and lead tracking. WebSocket pushes real-time status to the thank-you page.

**Tech Stack:** .NET 10, QuestPDF, Claude API (Anthropic SDK), Google Workspace CLI (`gws`), WebSocket, xUnit + Moq

**Design Doc:** `docs/plans/2026-03-09-cma-pipeline-design.md`

---

## Phase 1: API Scaffold & Domain Models

### Task 1: Scaffold .NET 10 Web API Project

**Files:**
- Create: `apps/api/RealEstateStar.Api/RealEstateStar.Api.csproj`
- Create: `apps/api/RealEstateStar.Api/Program.cs`
- Create: `apps/api/RealEstateStar.Api/appsettings.json`
- Create: `apps/api/RealEstateStar.Api/appsettings.Development.json`
- Create: `apps/api/RealEstateStar.Api.sln`
- Create: `apps/api/.gitignore`

**Step 1: Create the solution and API project**

```bash
cd apps/api
dotnet new sln -n RealEstateStar.Api
dotnet new webapi -n RealEstateStar.Api --no-https false --use-controllers false
dotnet sln add RealEstateStar.Api/RealEstateStar.Api.csproj
```

This creates a minimal API project (no controllers — we use minimal APIs).

**Step 2: Add NuGet packages**

```bash
cd apps/api/RealEstateStar.Api
dotnet add package QuestPDF --version 2025.*
dotnet add package Anthropic.SDK --version 3.*
dotnet add package Microsoft.AspNetCore.SignalR
```

**Step 3: Verify it builds and runs**

```bash
cd apps/api
dotnet build
dotnet run --project RealEstateStar.Api
```

Expected: API starts on `https://localhost:5001`, returns 200 on default endpoint.

**Step 4: Commit**

```bash
git add apps/api/
git commit -m "feat(api): scaffold .NET 10 Web API with QuestPDF and Anthropic SDK"
```

---

### Task 2: Create Test Project

**Files:**
- Create: `apps/api/RealEstateStar.Api.Tests/RealEstateStar.Api.Tests.csproj`
- Modify: `apps/api/RealEstateStar.Api.sln`

**Step 1: Create the test project**

```bash
cd apps/api
dotnet new xunit -n RealEstateStar.Api.Tests
dotnet sln add RealEstateStar.Api.Tests/RealEstateStar.Api.Tests.csproj
dotnet add RealEstateStar.Api.Tests reference RealEstateStar.Api/RealEstateStar.Api.csproj
cd RealEstateStar.Api.Tests
dotnet add package Moq --version 4.*
dotnet add package FluentAssertions --version 7.*
```

**Step 2: Verify tests run**

```bash
cd apps/api
dotnet test
```

Expected: 1 default test passes.

**Step 3: Commit**

```bash
git add apps/api/
git commit -m "test(api): add xUnit test project with Moq and FluentAssertions"
```

---

### Task 3: Domain Models — Lead, Comp, CmaJob

**Files:**
- Create: `apps/api/RealEstateStar.Api/Models/Lead.cs`
- Create: `apps/api/RealEstateStar.Api/Models/Comp.cs`
- Create: `apps/api/RealEstateStar.Api/Models/CmaJob.cs`
- Create: `apps/api/RealEstateStar.Api/Models/LeadResearch.cs`
- Create: `apps/api/RealEstateStar.Api/Models/CmaAnalysis.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Models/CmaJobTests.cs`

**Step 1: Write failing tests for CmaJob state transitions**

```csharp
// apps/api/RealEstateStar.Api.Tests/Models/CmaJobTests.cs
using FluentAssertions;
using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Tests.Models;

public class CmaJobTests
{
    [Fact]
    public void NewJob_HasParsingStatus()
    {
        var job = CmaJob.Create(Guid.NewGuid(), new Lead
        {
            FirstName = "John",
            LastName = "Smith",
            Email = "john@example.com",
            Phone = "555-0100",
            Address = "123 Main St",
            City = "Old Bridge",
            State = "NJ",
            Zip = "08857",
            Timeline = "6-12 months"
        });

        job.Status.Should().Be(CmaJobStatus.Parsing);
        job.Step.Should().Be(1);
        job.TotalSteps.Should().Be(9);
    }

    [Fact]
    public void AdvanceStep_UpdatesStatusAndStep()
    {
        var job = CmaJob.Create(Guid.NewGuid(), new Lead
        {
            FirstName = "John", LastName = "Smith",
            Email = "john@example.com", Phone = "555-0100",
            Address = "123 Main St", City = "Old Bridge",
            State = "NJ", Zip = "08857", Timeline = "ASAP"
        });

        job.AdvanceTo(CmaJobStatus.SearchingComps);

        job.Status.Should().Be(CmaJobStatus.SearchingComps);
        job.Step.Should().Be(2);
    }

    [Fact]
    public void ReportType_IsComprehensive_ForAsap()
    {
        var lead = new Lead { Timeline = "ASAP" };
        CmaJob.GetReportType(lead.Timeline).Should().Be(ReportType.Comprehensive);
    }

    [Fact]
    public void ReportType_IsLean_ForJustCurious()
    {
        var lead = new Lead { Timeline = "Just curious" };
        CmaJob.GetReportType(lead.Timeline).Should().Be(ReportType.Lean);
    }

    [Fact]
    public void ReportType_IsStandard_For6To12Months()
    {
        var lead = new Lead { Timeline = "6-12 months" };
        CmaJob.GetReportType(lead.Timeline).Should().Be(ReportType.Standard);
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
cd apps/api && dotnet test --filter "CmaJobTests"
```

Expected: FAIL — types don't exist yet.

**Step 3: Implement domain models**

```csharp
// apps/api/RealEstateStar.Api/Models/Lead.cs
namespace RealEstateStar.Api.Models;

public class Lead
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string Zip { get; set; } = "";
    public string Timeline { get; set; } = "";
    public int? Beds { get; set; }
    public int? Baths { get; set; }
    public int? Sqft { get; set; }
    public string? Notes { get; set; }

    public string FullName => $"{FirstName} {LastName}";
    public string FullAddress => $"{Address}, {City}, {State} {Zip}";
}
```

```csharp
// apps/api/RealEstateStar.Api/Models/Comp.cs
namespace RealEstateStar.Api.Models;

public class Comp
{
    public string Address { get; set; } = "";
    public decimal SalePrice { get; set; }
    public DateOnly SaleDate { get; set; }
    public int Beds { get; set; }
    public int Baths { get; set; }
    public int Sqft { get; set; }
    public decimal PricePerSqft => Sqft > 0 ? SalePrice / Sqft : 0;
    public int? DaysOnMarket { get; set; }
    public double DistanceMiles { get; set; }
    public CompSource Source { get; set; }
}

public enum CompSource
{
    Mls,
    Api,
    Zillow,
    RealtorCom,
    Redfin
}
```

```csharp
// apps/api/RealEstateStar.Api/Models/LeadResearch.cs
namespace RealEstateStar.Api.Models;

public class LeadResearch
{
    public string? Occupation { get; set; }
    public string? Employer { get; set; }
    public string? LinkedInUrl { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public decimal? PurchasePrice { get; set; }
    public decimal? TaxAssessment { get; set; }
    public decimal? AnnualPropertyTax { get; set; }
    public decimal? EstimatedEquityLow { get; set; }
    public decimal? EstimatedEquityHigh { get; set; }
    public string? LifeEventInsight { get; set; }
    public int? YearBuilt { get; set; }
    public decimal? LotSize { get; set; }
    public string? LotSizeUnit { get; set; }
    public string? NeighborhoodContext { get; set; }
}
```

```csharp
// apps/api/RealEstateStar.Api/Models/CmaAnalysis.cs
namespace RealEstateStar.Api.Models;

public class CmaAnalysis
{
    public decimal ValueLow { get; set; }
    public decimal ValueMid { get; set; }
    public decimal ValueHigh { get; set; }
    public string MarketNarrative { get; set; } = "";
    public string? PricingRecommendation { get; set; }
    public string? LeadInsights { get; set; }
    public List<string> ConversationStarters { get; set; } = [];
    public string MarketTrend { get; set; } = ""; // "Buyer's", "Seller's", "Balanced"
    public int MedianDaysOnMarket { get; set; }
}
```

```csharp
// apps/api/RealEstateStar.Api/Models/CmaJob.cs
namespace RealEstateStar.Api.Models;

public class CmaJob
{
    public Guid Id { get; private set; }
    public Guid AgentId { get; private set; }
    public Lead Lead { get; private set; } = null!;
    public CmaJobStatus Status { get; private set; }
    public int Step { get; private set; }
    public int TotalSteps => 9;
    public ReportType ReportType { get; private set; }
    public List<Comp> Comps { get; set; } = [];
    public LeadResearch? LeadResearch { get; set; }
    public CmaAnalysis? Analysis { get; set; }
    public string? PdfPath { get; set; }
    public string? DriveLink { get; set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; set; }

    public static CmaJob Create(Guid agentId, Lead lead)
    {
        return new CmaJob
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            Lead = lead,
            Status = CmaJobStatus.Parsing,
            Step = 1,
            ReportType = GetReportType(lead.Timeline),
            CreatedAt = DateTime.UtcNow
        };
    }

    public void AdvanceTo(CmaJobStatus status)
    {
        Status = status;
        Step = (int)status + 1;
    }

    public static ReportType GetReportType(string timeline) => timeline switch
    {
        "Just curious" => ReportType.Lean,
        "6-12 months" => ReportType.Standard,
        "3-6 months" => ReportType.Standard,
        "1-3 months" => ReportType.Comprehensive,
        "ASAP" => ReportType.Comprehensive,
        _ => ReportType.Standard
    };
}

public enum CmaJobStatus
{
    Parsing = 0,
    SearchingComps = 1,
    ResearchingLead = 2,
    Analyzing = 3,
    GeneratingPdf = 4,
    OrganizingDrive = 5,
    SendingEmail = 6,
    Logging = 7,
    Complete = 8
}

public enum ReportType
{
    Lean,
    Standard,
    Comprehensive
}
```

**Step 4: Run tests to verify they pass**

```bash
cd apps/api && dotnet test --filter "CmaJobTests"
```

Expected: All 5 tests PASS.

**Step 5: Commit**

```bash
git add apps/api/
git commit -m "feat(api): add CMA domain models — Lead, Comp, CmaJob, LeadResearch, CmaAnalysis"
```

---

## Phase 2: Agent Config Loading

### Task 4: Agent Config Loader Service

**Files:**
- Create: `apps/api/RealEstateStar.Api/Models/AgentConfig.cs`
- Create: `apps/api/RealEstateStar.Api/Services/IAgentConfigService.cs`
- Create: `apps/api/RealEstateStar.Api/Services/AgentConfigService.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Services/AgentConfigServiceTests.cs`

**Step 1: Write failing tests**

```csharp
// apps/api/RealEstateStar.Api.Tests/Services/AgentConfigServiceTests.cs
using FluentAssertions;
using RealEstateStar.Api.Services;

namespace RealEstateStar.Api.Tests.Services;

public class AgentConfigServiceTests
{
    [Fact]
    public async Task LoadAgent_ReturnsConfig_ForValidId()
    {
        var service = new AgentConfigService("../../../../config/agents");
        var config = await service.GetAgentAsync("jenise-buckalew");

        config.Should().NotBeNull();
        config!.Id.Should().Be("jenise-buckalew");
        config.Identity.Name.Should().Be("Jenise Buckalew");
        config.Location.State.Should().Be("NJ");
        config.Branding.PrimaryColor.Should().Be("#1B5E20");
    }

    [Fact]
    public async Task LoadAgent_ReturnsNull_ForUnknownId()
    {
        var service = new AgentConfigService("../../../../config/agents");
        var config = await service.GetAgentAsync("nonexistent-agent");

        config.Should().BeNull();
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
cd apps/api && dotnet test --filter "AgentConfigServiceTests"
```

Expected: FAIL — types don't exist.

**Step 3: Implement AgentConfig model and service**

The `AgentConfig` model mirrors `config/agent.schema.json`. The service reads JSON files from the `config/agents/` directory.

```csharp
// apps/api/RealEstateStar.Api/Models/AgentConfig.cs
using System.Text.Json.Serialization;

namespace RealEstateStar.Api.Models;

public class AgentConfig
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("identity")]
    public AgentIdentity Identity { get; set; } = new();

    [JsonPropertyName("location")]
    public AgentLocation Location { get; set; } = new();

    [JsonPropertyName("branding")]
    public AgentBranding Branding { get; set; } = new();

    [JsonPropertyName("integrations")]
    public AgentIntegrations Integrations { get; set; } = new();

    [JsonPropertyName("compliance")]
    public AgentCompliance Compliance { get; set; } = new();
}

public class AgentIdentity
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("license_id")]
    public string LicenseId { get; set; } = "";

    [JsonPropertyName("brokerage")]
    public string Brokerage { get; set; } = "";

    [JsonPropertyName("brokerage_id")]
    public string BrokerageId { get; set; } = "";

    [JsonPropertyName("phone")]
    public string Phone { get; set; } = "";

    [JsonPropertyName("email")]
    public string Email { get; set; } = "";

    [JsonPropertyName("website")]
    public string Website { get; set; } = "";

    [JsonPropertyName("languages")]
    public List<string> Languages { get; set; } = [];

    [JsonPropertyName("tagline")]
    public string Tagline { get; set; } = "";
}

public class AgentLocation
{
    [JsonPropertyName("state")]
    public string State { get; set; } = "";

    [JsonPropertyName("office_address")]
    public string OfficeAddress { get; set; } = "";

    [JsonPropertyName("service_areas")]
    public List<string> ServiceAreas { get; set; } = [];
}

public class AgentBranding
{
    [JsonPropertyName("primary_color")]
    public string PrimaryColor { get; set; } = "";

    [JsonPropertyName("secondary_color")]
    public string SecondaryColor { get; set; } = "";

    [JsonPropertyName("accent_color")]
    public string AccentColor { get; set; } = "";

    [JsonPropertyName("font_family")]
    public string FontFamily { get; set; } = "";
}

public class AgentIntegrations
{
    [JsonPropertyName("email_provider")]
    public string EmailProvider { get; set; } = "";

    [JsonPropertyName("form_handler")]
    public string FormHandler { get; set; } = "";

    [JsonPropertyName("form_handler_id")]
    public string? FormHandlerId { get; set; }
}

public class AgentCompliance
{
    [JsonPropertyName("state_form")]
    public string StateForm { get; set; } = "";

    [JsonPropertyName("licensing_body")]
    public string LicensingBody { get; set; } = "";

    [JsonPropertyName("disclosure_requirements")]
    public List<string> DisclosureRequirements { get; set; } = [];
}
```

```csharp
// apps/api/RealEstateStar.Api/Services/IAgentConfigService.cs
using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Services;

public interface IAgentConfigService
{
    Task<AgentConfig?> GetAgentAsync(string agentId);
}
```

```csharp
// apps/api/RealEstateStar.Api/Services/AgentConfigService.cs
using System.Text.Json;
using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Services;

public class AgentConfigService : IAgentConfigService
{
    private readonly string _configDir;

    public AgentConfigService(string configDir)
    {
        _configDir = configDir;
    }

    public async Task<AgentConfig?> GetAgentAsync(string agentId)
    {
        var path = Path.Combine(_configDir, $"{agentId}.json");
        if (!File.Exists(path)) return null;

        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<AgentConfig>(json);
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
cd apps/api && dotnet test --filter "AgentConfigServiceTests"
```

Expected: Both tests PASS.

**Step 5: Commit**

```bash
git add apps/api/
git commit -m "feat(api): add agent config loader — reads JSON profiles from config/agents/"
```

---

## Phase 3: Comp Data Sourcing

### Task 5: Comp Service Interface & Deduplication

**Files:**
- Create: `apps/api/RealEstateStar.Api/Services/Comps/ICompSource.cs`
- Create: `apps/api/RealEstateStar.Api/Services/Comps/CompAggregator.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Services/Comps/CompAggregatorTests.cs`

**Step 1: Write failing tests for deduplication and merge logic**

```csharp
// apps/api/RealEstateStar.Api.Tests/Services/Comps/CompAggregatorTests.cs
using FluentAssertions;
using Moq;
using RealEstateStar.Api.Models;
using RealEstateStar.Api.Services.Comps;

namespace RealEstateStar.Api.Tests.Services.Comps;

public class CompAggregatorTests
{
    [Fact]
    public async Task Aggregate_DeduplicatesByAddressAndSaleDate()
    {
        var comp = new Comp
        {
            Address = "100 Oak Ave, Old Bridge, NJ 08857",
            SalePrice = 450_000, SaleDate = new DateOnly(2025, 12, 1),
            Beds = 3, Baths = 2, Sqft = 1800, Source = CompSource.Zillow
        };
        var duplicate = new Comp
        {
            Address = "100 Oak Ave, Old Bridge, NJ 08857",
            SalePrice = 450_000, SaleDate = new DateOnly(2025, 12, 1),
            Beds = 3, Baths = 2, Sqft = 1800, Source = CompSource.RealtorCom
        };

        var source1 = MockSource(comp);
        var source2 = MockSource(duplicate);

        var aggregator = new CompAggregator([source1.Object, source2.Object]);
        var results = await aggregator.FetchCompsAsync("123 Main St", "Old Bridge", "NJ", "08857", 3, 2, 1800);

        results.Should().HaveCount(1);
    }

    [Fact]
    public async Task Aggregate_MlsWinsOnConflict()
    {
        var mlsComp = new Comp
        {
            Address = "100 Oak Ave, Old Bridge, NJ 08857",
            SalePrice = 455_000, SaleDate = new DateOnly(2025, 12, 1),
            Beds = 3, Baths = 2, Sqft = 1800, Source = CompSource.Mls
        };
        var zillowComp = new Comp
        {
            Address = "100 Oak Ave, Old Bridge, NJ 08857",
            SalePrice = 450_000, SaleDate = new DateOnly(2025, 12, 1),
            Beds = 3, Baths = 2, Sqft = 1800, Source = CompSource.Zillow
        };

        var mlsSource = MockSource(mlsComp);
        var zillowSource = MockSource(zillowComp);

        var aggregator = new CompAggregator([mlsSource.Object, zillowSource.Object]);
        var results = await aggregator.FetchCompsAsync("123 Main St", "Old Bridge", "NJ", "08857", 3, 2, 1800);

        results.Should().HaveCount(1);
        results[0].SalePrice.Should().Be(455_000);
        results[0].Source.Should().Be(CompSource.Mls);
    }

    [Fact]
    public async Task Aggregate_ContinuesIfOneSourceFails()
    {
        var goodComp = new Comp
        {
            Address = "200 Elm St, Old Bridge, NJ 08857",
            SalePrice = 400_000, SaleDate = new DateOnly(2025, 11, 15),
            Beds = 3, Baths = 2, Sqft = 1600, Source = CompSource.Redfin
        };

        var failingSource = new Mock<ICompSource>();
        failingSource.Setup(s => s.FetchAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(),
            It.IsAny<int?>(), It.IsAny<int?>()))
            .ThrowsAsync(new HttpRequestException("MLS down"));

        var goodSource = MockSource(goodComp);

        var aggregator = new CompAggregator([failingSource.Object, goodSource.Object]);
        var results = await aggregator.FetchCompsAsync("123 Main St", "Old Bridge", "NJ", "08857", 3, 2, 1800);

        results.Should().HaveCount(1);
        results[0].Address.Should().Contain("200 Elm");
    }

    private static Mock<ICompSource> MockSource(params Comp[] comps)
    {
        var mock = new Mock<ICompSource>();
        mock.Setup(s => s.FetchAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(),
            It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(comps.ToList());
        return mock;
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
cd apps/api && dotnet test --filter "CompAggregatorTests"
```

Expected: FAIL — interfaces and classes don't exist.

**Step 3: Implement ICompSource and CompAggregator**

```csharp
// apps/api/RealEstateStar.Api/Services/Comps/ICompSource.cs
using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Services.Comps;

public interface ICompSource
{
    string Name { get; }
    Task<List<Comp>> FetchAsync(string address, string city, string state, string zip,
        int? beds, int? baths, int? sqft);
}
```

```csharp
// apps/api/RealEstateStar.Api/Services/Comps/CompAggregator.cs
using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Services.Comps;

public class CompAggregator
{
    private readonly IEnumerable<ICompSource> _sources;

    // Source priority — MLS wins over API wins over scrape
    private static readonly Dictionary<CompSource, int> SourcePriority = new()
    {
        [CompSource.Mls] = 0,
        [CompSource.Api] = 1,
        [CompSource.Zillow] = 2,
        [CompSource.RealtorCom] = 3,
        [CompSource.Redfin] = 4
    };

    public CompAggregator(IEnumerable<ICompSource> sources)
    {
        _sources = sources;
    }

    public async Task<List<Comp>> FetchCompsAsync(string address, string city,
        string state, string zip, int? beds, int? baths, int? sqft)
    {
        // Query all sources in parallel
        var tasks = _sources.Select(async source =>
        {
            try
            {
                return await source.FetchAsync(address, city, state, zip, beds, baths, sqft);
            }
            catch
            {
                return new List<Comp>();
            }
        });

        var results = await Task.WhenAll(tasks);
        var allComps = results.SelectMany(r => r).ToList();

        return Deduplicate(allComps);
    }

    private static List<Comp> Deduplicate(List<Comp> comps)
    {
        return comps
            .GroupBy(c => NormalizeKey(c.Address, c.SaleDate))
            .Select(g => g.OrderBy(c => SourcePriority.GetValueOrDefault(c.Source, 99)).First())
            .ToList();
    }

    private static string NormalizeKey(string address, DateOnly saleDate)
    {
        var normalized = address.Trim().ToUpperInvariant()
            .Replace(".", "").Replace(",", "").Replace("  ", " ");
        return $"{normalized}|{saleDate:yyyy-MM-dd}";
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
cd apps/api && dotnet test --filter "CompAggregatorTests"
```

Expected: All 3 tests PASS.

**Step 5: Commit**

```bash
git add apps/api/
git commit -m "feat(api): add comp aggregator — parallel fetch, dedup, MLS-wins merge"
```

---

### Task 6: Zillow Scraping Comp Source (First Implementation)

**Files:**
- Create: `apps/api/RealEstateStar.Api/Services/Comps/ZillowCompSource.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Services/Comps/ZillowCompSourceTests.cs`

**Step 1: Write failing test for HTML parsing**

```csharp
// apps/api/RealEstateStar.Api.Tests/Services/Comps/ZillowCompSourceTests.cs
using FluentAssertions;
using RealEstateStar.Api.Models;
using RealEstateStar.Api.Services.Comps;

namespace RealEstateStar.Api.Tests.Services.Comps;

public class ZillowCompSourceTests
{
    [Fact]
    public void ParseComps_ExtractsCompFromHtml()
    {
        // This tests the HTML parsing logic with a sample fixture.
        // The actual HTTP call is mocked in integration tests.
        var source = new ZillowCompSource(new HttpClient());
        source.Name.Should().Be("Zillow");
    }
}
```

> **Note to implementer:** The Zillow, Realtor.com, and Redfin scrapers are structurally similar. Implement Zillow first as the pattern, then replicate for the other two. The actual scraping logic will need to be adapted as site structures change — isolate HTML parsing into testable methods. Use `HttpClient` with proper User-Agent headers and rate limiting.

**Step 2: Implement stub Zillow source**

```csharp
// apps/api/RealEstateStar.Api/Services/Comps/ZillowCompSource.cs
using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Services.Comps;

public class ZillowCompSource : ICompSource
{
    private readonly HttpClient _http;
    public string Name => "Zillow";

    public ZillowCompSource(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<Comp>> FetchAsync(string address, string city,
        string state, string zip, int? beds, int? baths, int? sqft)
    {
        // Build search URL from address components
        var slug = $"{city}-{state}-{zip}".Replace(" ", "-").ToLower();
        var url = $"https://www.zillow.com/homes/recently-sold/{slug}_rb/";

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync();
            return ParseComps(html, state);
        }
        catch
        {
            return [];
        }
    }

    internal List<Comp> ParseComps(string html, string state)
    {
        // TODO: Implement HTML parsing — extract property cards with
        // address, price, beds, baths, sqft, sale date.
        // Use regex or AngleSharp for structured parsing.
        // This will need periodic maintenance as Zillow changes their HTML.
        return [];
    }
}
```

**Step 3: Run tests**

```bash
cd apps/api && dotnet test --filter "ZillowCompSourceTests"
```

Expected: PASS (stub test).

**Step 4: Commit**

```bash
git add apps/api/
git commit -m "feat(api): add Zillow comp source stub — HTTP fetch + HTML parse skeleton"
```

---

### Task 7: Realtor.com and Redfin Comp Sources

**Files:**
- Create: `apps/api/RealEstateStar.Api/Services/Comps/RealtorComCompSource.cs`
- Create: `apps/api/RealEstateStar.Api/Services/Comps/RedfinCompSource.cs`
- Create: `apps/api/RealEstateStar.Api/Services/Comps/AttomDataCompSource.cs`

Follow the same pattern as Task 6. Each source implements `ICompSource` with site-specific URL building and HTML parsing. `AttomDataCompSource` uses a REST API instead of scraping.

**Step 1: Create all three source stubs**

Each follows the ZillowCompSource pattern:
- `RealtorComCompSource` — scrapes realtor.com recently-sold pages
- `RedfinCompSource` — scrapes redfin.com sold homes
- `AttomDataCompSource` — calls ATTOM Data API (structured JSON, requires API key)

**Step 2: Run tests**

```bash
cd apps/api && dotnet test
```

Expected: All tests PASS.

**Step 3: Commit**

```bash
git add apps/api/
git commit -m "feat(api): add Realtor.com, Redfin, and ATTOM Data comp sources"
```

---

## Phase 4: Lead Research

### Task 8: Lead Research Service

**Files:**
- Create: `apps/api/RealEstateStar.Api/Services/Research/ILeadResearchService.cs`
- Create: `apps/api/RealEstateStar.Api/Services/Research/LeadResearchService.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Services/Research/LeadResearchServiceTests.cs`

**Step 1: Write failing tests**

```csharp
// apps/api/RealEstateStar.Api.Tests/Services/Research/LeadResearchServiceTests.cs
using FluentAssertions;
using Moq;
using RealEstateStar.Api.Models;
using RealEstateStar.Api.Services.Research;

namespace RealEstateStar.Api.Tests.Services.Research;

public class LeadResearchServiceTests
{
    [Fact]
    public async Task Research_ReturnsPartialData_WhenSomeSourcesFail()
    {
        var mockHttp = new Mock<HttpClient>();
        var service = new LeadResearchService(mockHttp.Object);

        var lead = new Lead
        {
            FirstName = "John", LastName = "Smith",
            Address = "123 Main St", City = "Old Bridge",
            State = "NJ", Zip = "08857"
        };

        var result = await service.ResearchAsync(lead);

        // Should return a LeadResearch object even if all sources fail
        result.Should().NotBeNull();
    }

    [Fact]
    public void CalculateOwnershipDuration_ReturnsYears()
    {
        var purchaseDate = new DateOnly(2019, 3, 15);
        var duration = LeadResearchService.CalculateOwnershipDuration(purchaseDate);

        duration.Should().Contain("year");
    }

    [Fact]
    public void EstimateEquity_CalculatesFromPurchaseAndCurrentValue()
    {
        var result = LeadResearchService.EstimateEquity(
            purchasePrice: 350_000m,
            currentValueMid: 450_000m);

        result.low.Should().BeGreaterThan(0);
        result.high.Should().BeGreaterThan(result.low);
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
cd apps/api && dotnet test --filter "LeadResearchServiceTests"
```

Expected: FAIL.

**Step 3: Implement the service**

```csharp
// apps/api/RealEstateStar.Api/Services/Research/ILeadResearchService.cs
using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Services.Research;

public interface ILeadResearchService
{
    Task<LeadResearch> ResearchAsync(Lead lead);
}
```

```csharp
// apps/api/RealEstateStar.Api/Services/Research/LeadResearchService.cs
using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Services.Research;

public class LeadResearchService : ILeadResearchService
{
    private readonly HttpClient _http;

    public LeadResearchService(HttpClient http)
    {
        _http = http;
    }

    public async Task<LeadResearch> ResearchAsync(Lead lead)
    {
        var research = new LeadResearch();

        // Run all research tasks in parallel — each catches its own errors
        var tasks = new List<Task>
        {
            ResearchPublicRecordsAsync(lead, research),
            ResearchLinkedInAsync(lead, research),
            ResearchNeighborhoodAsync(lead, research)
        };

        await Task.WhenAll(tasks);
        return research;
    }

    private async Task ResearchPublicRecordsAsync(Lead lead, LeadResearch research)
    {
        try
        {
            // Search county tax records and public deed records
            // for purchase date, purchase price, tax assessment
            // TODO: Implement county assessor lookup by address + state
        }
        catch { /* Source failure doesn't block pipeline */ }
    }

    private async Task ResearchLinkedInAsync(Lead lead, LeadResearch research)
    {
        try
        {
            // Search LinkedIn for name + location to find occupation/employer
            // TODO: Implement public LinkedIn profile search
        }
        catch { /* Source failure doesn't block pipeline */ }
    }

    private async Task ResearchNeighborhoodAsync(Lead lead, LeadResearch research)
    {
        try
        {
            // Search for school ratings, walkability, local amenities
            // TODO: Implement neighborhood context lookup
        }
        catch { /* Source failure doesn't block pipeline */ }
    }

    public static string CalculateOwnershipDuration(DateOnly purchaseDate)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var years = today.Year - purchaseDate.Year;
        if (today.DayOfYear < purchaseDate.DayOfYear) years--;
        return years == 1 ? "1 year" : $"{years} years";
    }

    public static (decimal low, decimal high) EstimateEquity(
        decimal purchasePrice, decimal currentValueMid)
    {
        // Conservative: assume 80% LTV at purchase, standard amortization
        var estimatedRemainingMortgage = purchasePrice * 0.65m; // rough after years of payments
        var equityMid = currentValueMid - estimatedRemainingMortgage;
        return (equityMid * 0.85m, equityMid * 1.15m);
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
cd apps/api && dotnet test --filter "LeadResearchServiceTests"
```

Expected: All 3 tests PASS.

**Step 5: Commit**

```bash
git add apps/api/
git commit -m "feat(api): add lead research service — public records, LinkedIn, neighborhood"
```

---

## Phase 5: Claude API Analysis

### Task 9: Claude Analysis Service

**Files:**
- Create: `apps/api/RealEstateStar.Api/Services/Analysis/IAnalysisService.cs`
- Create: `apps/api/RealEstateStar.Api/Services/Analysis/ClaudeAnalysisService.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Services/Analysis/ClaudeAnalysisServiceTests.cs`

**Step 1: Write failing tests**

```csharp
// apps/api/RealEstateStar.Api.Tests/Services/Analysis/ClaudeAnalysisServiceTests.cs
using FluentAssertions;
using RealEstateStar.Api.Models;
using RealEstateStar.Api.Services.Analysis;

namespace RealEstateStar.Api.Tests.Services.Analysis;

public class ClaudeAnalysisServiceTests
{
    [Fact]
    public void BuildPrompt_IncludesAllCompData()
    {
        var lead = new Lead
        {
            FirstName = "John", LastName = "Smith",
            Address = "123 Main St", City = "Old Bridge",
            State = "NJ", Zip = "08857", Timeline = "ASAP",
            Beds = 3, Baths = 2, Sqft = 1800
        };
        var comps = new List<Comp>
        {
            new() { Address = "100 Oak Ave", SalePrice = 450_000,
                SaleDate = new DateOnly(2025, 12, 1), Beds = 3,
                Baths = 2, Sqft = 1800, Source = CompSource.Mls }
        };

        var prompt = ClaudeAnalysisService.BuildPrompt(lead, comps, null, ReportType.Comprehensive);

        prompt.Should().Contain("123 Main St");
        prompt.Should().Contain("450,000");
        prompt.Should().Contain("ASAP");
        prompt.Should().Contain("Comprehensive");
    }

    [Fact]
    public void BuildPrompt_IncludesLeadResearch_WhenAvailable()
    {
        var lead = new Lead
        {
            FirstName = "John", LastName = "Smith",
            Address = "123 Main St", City = "Old Bridge",
            State = "NJ", Zip = "08857", Timeline = "1-3 months"
        };
        var research = new LeadResearch
        {
            Occupation = "Software Engineer",
            Employer = "Google",
            PurchasePrice = 300_000,
            PurchaseDate = new DateOnly(2018, 5, 1)
        };

        var prompt = ClaudeAnalysisService.BuildPrompt(lead, [], research, ReportType.Comprehensive);

        prompt.Should().Contain("Software Engineer");
        prompt.Should().Contain("Google");
        prompt.Should().Contain("300,000");
    }

    [Fact]
    public void ParseResponse_ExtractsStructuredAnalysis()
    {
        var json = """
        {
            "valueLow": 420000,
            "valueMid": 445000,
            "valueHigh": 470000,
            "marketNarrative": "The Old Bridge market is strong.",
            "pricingRecommendation": "List at $449,900",
            "leadInsights": "Owned for 7 years, significant equity.",
            "conversationStarters": ["Ask about their equity growth", "Mention school district demand"],
            "marketTrend": "Seller's",
            "medianDaysOnMarket": 18
        }
        """;

        var analysis = ClaudeAnalysisService.ParseResponse(json);

        analysis.ValueMid.Should().Be(445_000);
        analysis.MarketTrend.Should().Be("Seller's");
        analysis.ConversationStarters.Should().HaveCount(2);
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
cd apps/api && dotnet test --filter "ClaudeAnalysisServiceTests"
```

Expected: FAIL.

**Step 3: Implement the service**

```csharp
// apps/api/RealEstateStar.Api/Services/Analysis/IAnalysisService.cs
using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Services.Analysis;

public interface IAnalysisService
{
    Task<CmaAnalysis> AnalyzeAsync(Lead lead, List<Comp> comps,
        LeadResearch? research, ReportType reportType);
}
```

```csharp
// apps/api/RealEstateStar.Api/Services/Analysis/ClaudeAnalysisService.cs
using System.Text.Json;
using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Services.Analysis;

public class ClaudeAnalysisService : IAnalysisService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public ClaudeAnalysisService(HttpClient http, string apiKey)
    {
        _http = http;
        _apiKey = apiKey;
    }

    public async Task<CmaAnalysis> AnalyzeAsync(Lead lead, List<Comp> comps,
        LeadResearch? research, ReportType reportType)
    {
        var prompt = BuildPrompt(lead, comps, research, reportType);

        var request = new HttpRequestMessage(HttpMethod.Post,
            "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = JsonContent.Create(new
        {
            model = "claude-sonnet-4-6",
            max_tokens = 4096,
            messages = new[] { new { role = "user", content = prompt } }
        });

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        var messageResponse = JsonDocument.Parse(body);
        var content = messageResponse.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? "{}";

        return ParseResponse(content);
    }

    public static string BuildPrompt(Lead lead, List<Comp> comps,
        LeadResearch? research, ReportType reportType)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("You are a real estate market analyst. Analyze the following data and return a JSON response.");
        sb.AppendLine();
        sb.AppendLine($"## Subject Property");
        sb.AppendLine($"Address: {lead.FullAddress}");
        if (lead.Beds.HasValue) sb.AppendLine($"Beds: {lead.Beds}");
        if (lead.Baths.HasValue) sb.AppendLine($"Baths: {lead.Baths}");
        if (lead.Sqft.HasValue) sb.AppendLine($"Sqft: {lead.Sqft}");
        sb.AppendLine($"Seller Timeline: {lead.Timeline}");
        sb.AppendLine($"Report Type: {reportType}");
        sb.AppendLine();

        sb.AppendLine("## Comparable Sales");
        foreach (var comp in comps)
        {
            sb.AppendLine($"- {comp.Address}: ${comp.SalePrice:N0}, {comp.Beds}bd/{comp.Baths}ba, " +
                $"{comp.Sqft}sqft, ${comp.PricePerSqft:N0}/sqft, sold {comp.SaleDate:yyyy-MM-dd}, " +
                $"{comp.DaysOnMarket} DOM, source: {comp.Source}");
        }
        sb.AppendLine();

        if (research != null)
        {
            sb.AppendLine("## Lead Research");
            if (research.Occupation != null)
                sb.AppendLine($"Occupation: {research.Occupation} at {research.Employer}");
            if (research.PurchasePrice.HasValue)
                sb.AppendLine($"Purchase Price: ${research.PurchasePrice:N0}");
            if (research.PurchaseDate.HasValue)
                sb.AppendLine($"Purchase Date: {research.PurchaseDate:yyyy-MM-dd}");
            if (research.TaxAssessment.HasValue)
                sb.AppendLine($"Tax Assessment: ${research.TaxAssessment:N0}");
            if (research.LifeEventInsight != null)
                sb.AppendLine($"Life Event: {research.LifeEventInsight}");
            sb.AppendLine();
        }

        sb.AppendLine("## Instructions");
        sb.AppendLine("Return ONLY valid JSON with this structure:");
        sb.AppendLine("""
        {
            "valueLow": number,
            "valueMid": number,
            "valueHigh": number,
            "marketNarrative": "string (2-4 paragraphs adapted to timeline/report type)",
            "pricingRecommendation": "string or null (only for 1-3 months/ASAP timelines)",
            "leadInsights": "string or null (summary of lead research findings)",
            "conversationStarters": ["string", "string", "string"],
            "marketTrend": "Buyer's" | "Seller's" | "Balanced",
            "medianDaysOnMarket": number
        }
        """);

        return sb.ToString();
    }

    public static CmaAnalysis ParseResponse(string json)
    {
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new CmaAnalysis
        {
            ValueLow = root.GetProperty("valueLow").GetDecimal(),
            ValueMid = root.GetProperty("valueMid").GetDecimal(),
            ValueHigh = root.GetProperty("valueHigh").GetDecimal(),
            MarketNarrative = root.GetProperty("marketNarrative").GetString() ?? "",
            PricingRecommendation = root.TryGetProperty("pricingRecommendation", out var pr)
                ? pr.GetString() : null,
            LeadInsights = root.TryGetProperty("leadInsights", out var li)
                ? li.GetString() : null,
            ConversationStarters = root.TryGetProperty("conversationStarters", out var cs)
                ? cs.EnumerateArray().Select(e => e.GetString() ?? "").ToList()
                : [],
            MarketTrend = root.GetProperty("marketTrend").GetString() ?? "Balanced",
            MedianDaysOnMarket = root.GetProperty("medianDaysOnMarket").GetInt32()
        };
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
cd apps/api && dotnet test --filter "ClaudeAnalysisServiceTests"
```

Expected: All 3 tests PASS.

**Step 5: Commit**

```bash
git add apps/api/
git commit -m "feat(api): add Claude analysis service — prompt builder, JSON response parser"
```

---

## Phase 6: PDF Generation

### Task 10: QuestPDF CMA Report Generator

**Files:**
- Create: `apps/api/RealEstateStar.Api/Services/Pdf/CmaPdfGenerator.cs`
- Create: `apps/api/RealEstateStar.Api/Services/Pdf/ICmaPdfGenerator.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Services/Pdf/CmaPdfGeneratorTests.cs`

**Step 1: Write failing tests**

```csharp
// apps/api/RealEstateStar.Api.Tests/Services/Pdf/CmaPdfGeneratorTests.cs
using FluentAssertions;
using RealEstateStar.Api.Models;
using RealEstateStar.Api.Services.Pdf;

namespace RealEstateStar.Api.Tests.Services.Pdf;

public class CmaPdfGeneratorTests
{
    [Fact]
    public void Generate_CreatesFileOnDisk()
    {
        var generator = new CmaPdfGenerator();
        var agent = CreateTestAgent();
        var lead = CreateTestLead();
        var comps = CreateTestComps();
        var analysis = CreateTestAnalysis();
        var outputPath = Path.Combine(Path.GetTempPath(), $"CMA_test_{Guid.NewGuid()}.pdf");

        try
        {
            generator.Generate(outputPath, agent, lead, comps, analysis, null, ReportType.Lean);
            File.Exists(outputPath).Should().BeTrue();
            new FileInfo(outputPath).Length.Should().BeGreaterThan(0);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    [Fact]
    public void Generate_ComprehensiveReport_HasMorePages()
    {
        var generator = new CmaPdfGenerator();
        var agent = CreateTestAgent();
        var lead = CreateTestLead();
        var comps = CreateTestComps();
        var analysis = CreateTestAnalysis();
        var leanPath = Path.Combine(Path.GetTempPath(), $"CMA_lean_{Guid.NewGuid()}.pdf");
        var compPath = Path.Combine(Path.GetTempPath(), $"CMA_comp_{Guid.NewGuid()}.pdf");

        try
        {
            generator.Generate(leanPath, agent, lead, comps, analysis, null, ReportType.Lean);
            generator.Generate(compPath, agent, lead, comps, analysis,
                new LeadResearch { Occupation = "Engineer" }, ReportType.Comprehensive);

            new FileInfo(compPath).Length.Should().BeGreaterThan(new FileInfo(leanPath).Length);
        }
        finally
        {
            if (File.Exists(leanPath)) File.Delete(leanPath);
            if (File.Exists(compPath)) File.Delete(compPath);
        }
    }

    private static AgentConfig CreateTestAgent() => new()
    {
        Id = "test-agent",
        Identity = new() { Name = "Test Agent", Title = "REALTOR®",
            Brokerage = "Test Realty", Phone = "555-0100",
            Email = "test@test.com", Website = "test.com",
            Languages = ["English"], Tagline = "Test tagline" },
        Branding = new() { PrimaryColor = "#1B5E20", SecondaryColor = "#2E7D32",
            AccentColor = "#C8A951", FontFamily = "Helvetica" }
    };

    private static Lead CreateTestLead() => new()
    {
        FirstName = "John", LastName = "Smith",
        Address = "123 Main St", City = "Old Bridge",
        State = "NJ", Zip = "08857", Timeline = "ASAP",
        Beds = 3, Baths = 2, Sqft = 1800
    };

    private static List<Comp> CreateTestComps() =>
    [
        new() { Address = "100 Oak Ave, Old Bridge, NJ", SalePrice = 450_000,
            SaleDate = new DateOnly(2025, 12, 1), Beds = 3, Baths = 2,
            Sqft = 1800, DaysOnMarket = 15, Source = CompSource.Mls },
        new() { Address = "200 Elm St, Old Bridge, NJ", SalePrice = 430_000,
            SaleDate = new DateOnly(2025, 11, 15), Beds = 3, Baths = 2,
            Sqft = 1750, DaysOnMarket = 22, Source = CompSource.Zillow }
    ];

    private static CmaAnalysis CreateTestAnalysis() => new()
    {
        ValueLow = 420_000, ValueMid = 445_000, ValueHigh = 470_000,
        MarketNarrative = "The market is strong in Old Bridge.",
        MarketTrend = "Seller's", MedianDaysOnMarket = 18,
        ConversationStarters = ["Great equity position"]
    };
}
```

**Step 2: Run tests to verify they fail**

```bash
cd apps/api && dotnet test --filter "CmaPdfGeneratorTests"
```

Expected: FAIL.

**Step 3: Implement the PDF generator**

```csharp
// apps/api/RealEstateStar.Api/Services/Pdf/ICmaPdfGenerator.cs
using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Services.Pdf;

public interface ICmaPdfGenerator
{
    void Generate(string outputPath, AgentConfig agent, Lead lead,
        List<Comp> comps, CmaAnalysis analysis, LeadResearch? research,
        ReportType reportType);
}
```

```csharp
// apps/api/RealEstateStar.Api/Services/Pdf/CmaPdfGenerator.cs
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Services.Pdf;

public class CmaPdfGenerator : ICmaPdfGenerator
{
    public void Generate(string outputPath, AgentConfig agent, Lead lead,
        List<Comp> comps, CmaAnalysis analysis, LeadResearch? research,
        ReportType reportType)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(40);
                BuildCoverPage(page.Content(), agent, lead);
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });

            if (reportType != ReportType.Lean)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.Letter);
                    page.Margin(40);
                    BuildPropertyOverview(page.Content(), lead, research);
                });
            }

            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(40);
                BuildCompTable(page.Content(), comps);
            });

            if (reportType == ReportType.Comprehensive)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.Letter);
                    page.Margin(40);
                    BuildMarketAnalysis(page.Content(), analysis);
                });

                container.Page(page =>
                {
                    page.Size(PageSizes.Letter);
                    page.Margin(40);
                    BuildPricePerSqftAnalysis(page.Content(), comps, lead);
                });
            }

            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(40);
                BuildValueEstimate(page.Content(), analysis, reportType);
            });

            if (reportType == ReportType.Comprehensive)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.Letter);
                    page.Margin(40);
                    BuildNeighborhoodOverview(page.Content(), research);
                });
            }

            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(40);
                BuildAboutAgent(page.Content(), agent);
            });
        })
        .GeneratePdf(outputPath);
    }

    private static void BuildCoverPage(IContainer container, AgentConfig agent, Lead lead)
    {
        container.Column(col =>
        {
            col.Spacing(20);
            col.Item().PaddingTop(100).AlignCenter()
                .Text("Comparative Market Analysis").FontSize(28).Bold();
            col.Item().AlignCenter()
                .Text(lead.FullAddress).FontSize(16);
            col.Item().PaddingTop(40).AlignCenter()
                .Text($"Prepared for: {lead.FullName}").FontSize(14);
            col.Item().AlignCenter()
                .Text($"Prepared by: {agent.Identity.Name}, {agent.Identity.Title}").FontSize(14);
            col.Item().AlignCenter()
                .Text(agent.Identity.Brokerage).FontSize(12);
            col.Item().AlignCenter()
                .Text(DateTime.Today.ToString("MMMM d, yyyy")).FontSize(12);
            col.Item().PaddingTop(40).AlignCenter()
                .Text($"{agent.Identity.Phone} | {agent.Identity.Email} | {agent.Identity.Website}")
                .FontSize(10);
        });
    }

    private static void BuildPropertyOverview(IContainer container, Lead lead, LeadResearch? research)
    {
        container.Column(col =>
        {
            col.Spacing(10);
            col.Item().Text("Subject Property Overview").FontSize(20).Bold();
            col.Item().Text(lead.FullAddress).FontSize(14);
            if (lead.Beds.HasValue) col.Item().Text($"Bedrooms: {lead.Beds}");
            if (lead.Baths.HasValue) col.Item().Text($"Bathrooms: {lead.Baths}");
            if (lead.Sqft.HasValue) col.Item().Text($"Square Feet: {lead.Sqft:N0}");
            if (research?.YearBuilt != null) col.Item().Text($"Year Built: {research.YearBuilt}");
            if (research?.LotSize != null) col.Item().Text($"Lot Size: {research.LotSize} {research.LotSizeUnit}");
            if (research?.TaxAssessment != null) col.Item().Text($"Tax Assessment: ${research.TaxAssessment:N0}");
        });
    }

    private static void BuildCompTable(IContainer container, List<Comp> comps)
    {
        container.Column(col =>
        {
            col.Spacing(10);
            col.Item().Text("Comparable Sales").FontSize(20).Bold();
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3); // Address
                    columns.RelativeColumn(1.5f); // Price
                    columns.RelativeColumn(1); // Beds/Baths
                    columns.RelativeColumn(1); // Sqft
                    columns.RelativeColumn(1); // $/Sqft
                    columns.RelativeColumn(1.2f); // Date
                    columns.RelativeColumn(0.8f); // Source
                });

                table.Header(header =>
                {
                    header.Cell().Text("Address").Bold();
                    header.Cell().Text("Sale Price").Bold();
                    header.Cell().Text("Bd/Ba").Bold();
                    header.Cell().Text("Sqft").Bold();
                    header.Cell().Text("$/Sqft").Bold();
                    header.Cell().Text("Sale Date").Bold();
                    header.Cell().Text("Source").Bold();
                });

                foreach (var comp in comps)
                {
                    table.Cell().Text(comp.Address).FontSize(9);
                    table.Cell().Text($"${comp.SalePrice:N0}").FontSize(9);
                    table.Cell().Text($"{comp.Beds}/{comp.Baths}").FontSize(9);
                    table.Cell().Text($"{comp.Sqft:N0}").FontSize(9);
                    table.Cell().Text($"${comp.PricePerSqft:N0}").FontSize(9);
                    table.Cell().Text(comp.SaleDate.ToString("MM/dd/yy")).FontSize(9);
                    table.Cell().Text(comp.Source.ToString()).FontSize(9);
                }
            });
        });
    }

    private static void BuildMarketAnalysis(IContainer container, CmaAnalysis analysis)
    {
        container.Column(col =>
        {
            col.Spacing(10);
            col.Item().Text("Market Analysis").FontSize(20).Bold();
            col.Item().Text(analysis.MarketNarrative);
            col.Item().Text($"Market Trend: {analysis.MarketTrend}").Bold();
            col.Item().Text($"Median Days on Market: {analysis.MedianDaysOnMarket}");
        });
    }

    private static void BuildPricePerSqftAnalysis(IContainer container, List<Comp> comps, Lead lead)
    {
        container.Column(col =>
        {
            col.Spacing(10);
            col.Item().Text("Price Per Square Foot Analysis").FontSize(20).Bold();
            foreach (var comp in comps)
            {
                col.Item().Text($"{comp.Address}: ${comp.PricePerSqft:N0}/sqft");
            }
            if (comps.Count > 0)
            {
                var avgPpsf = comps.Average(c => c.PricePerSqft);
                col.Item().PaddingTop(10).Text($"Average: ${avgPpsf:N0}/sqft").Bold();
                if (lead.Sqft.HasValue)
                {
                    col.Item().Text($"Subject ({lead.Sqft:N0} sqft) x ${avgPpsf:N0}/sqft = ${avgPpsf * lead.Sqft.Value:N0}");
                }
            }
        });
    }

    private static void BuildValueEstimate(IContainer container, CmaAnalysis analysis, ReportType reportType)
    {
        container.Column(col =>
        {
            col.Spacing(10);
            col.Item().Text("Estimated Value").FontSize(20).Bold();
            col.Item().Text($"Low: ${analysis.ValueLow:N0}").FontSize(14);
            col.Item().Text($"Estimated: ${analysis.ValueMid:N0}").FontSize(18).Bold();
            col.Item().Text($"High: ${analysis.ValueHigh:N0}").FontSize(14);
            if (reportType == ReportType.Comprehensive && analysis.PricingRecommendation != null)
            {
                col.Item().PaddingTop(15).Text("Pricing Strategy").FontSize(16).Bold();
                col.Item().Text(analysis.PricingRecommendation);
            }
        });
    }

    private static void BuildNeighborhoodOverview(IContainer container, LeadResearch? research)
    {
        container.Column(col =>
        {
            col.Spacing(10);
            col.Item().Text("Neighborhood Overview").FontSize(20).Bold();
            if (research?.NeighborhoodContext != null)
                col.Item().Text(research.NeighborhoodContext);
            else
                col.Item().Text("Neighborhood data is being compiled for this area.");
        });
    }

    private static void BuildAboutAgent(IContainer container, AgentConfig agent)
    {
        container.Column(col =>
        {
            col.Spacing(10);
            col.Item().Text($"About {agent.Identity.Name}").FontSize(20).Bold();
            col.Item().Text($"{agent.Identity.Name}, {agent.Identity.Title}");
            col.Item().Text(agent.Identity.Brokerage);
            if (agent.Location.ServiceAreas.Count > 0)
                col.Item().Text($"Serving: {string.Join(", ", agent.Location.ServiceAreas)}");
            if (agent.Identity.Languages.Count > 1)
                col.Item().Text($"Languages: {string.Join(", ", agent.Identity.Languages)}");
            col.Item().PaddingTop(10).Text(agent.Identity.Tagline).Italic();
            col.Item().PaddingTop(20).Text("Ready to take the next step?").FontSize(14).Bold();
            col.Item().Text("Schedule a listing consultation today.");
            col.Item().Text($"{agent.Identity.Phone} | {agent.Identity.Email}");
        });
    }
}
```

> **Note:** This is functional but basic. The QuestPDF fluent API supports much richer styling — agent branding colors, backgrounds, images, decorative elements. Enhance visuals iteratively after the pipeline is working end-to-end.

**Step 4: Run tests to verify they pass**

```bash
cd apps/api && dotnet test --filter "CmaPdfGeneratorTests"
```

Expected: Both tests PASS.

**Step 5: Commit**

```bash
git add apps/api/
git commit -m "feat(api): add QuestPDF CMA report generator — adaptive lean/standard/comprehensive"
```

---

## Phase 7: Google Workspace Integration

### Task 11: GWS Service Wrapper

**Files:**
- Create: `apps/api/RealEstateStar.Api/Services/Gws/IGwsService.cs`
- Create: `apps/api/RealEstateStar.Api/Services/Gws/GwsService.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Services/Gws/GwsServiceTests.cs`

The `gws` CLI is called as a subprocess. This service wraps the shell calls.

**Step 1: Write failing tests**

```csharp
// apps/api/RealEstateStar.Api.Tests/Services/Gws/GwsServiceTests.cs
using FluentAssertions;
using RealEstateStar.Api.Services.Gws;

namespace RealEstateStar.Api.Tests.Services.Gws;

public class GwsServiceTests
{
    [Fact]
    public void BuildDriveFolderPath_FormatsCorrectly()
    {
        var path = GwsService.BuildLeadFolderPath("John Smith", "123 Main St, Old Bridge NJ");
        path.Should().Be("Real Estate Star/1 - Leads/John Smith/123 Main St, Old Bridge NJ");
    }

    [Fact]
    public void BuildLeadBriefContent_IncludesAllSections()
    {
        var content = GwsService.BuildLeadBriefContent(
            leadName: "John Smith",
            address: "123 Main St, Old Bridge, NJ 08857",
            timeline: "ASAP",
            submittedAt: new DateTime(2026, 3, 9, 14, 30, 0),
            occupation: "Software Engineer",
            employer: "Google",
            purchaseDate: new DateOnly(2019, 5, 1),
            purchasePrice: 300_000m,
            ownershipDuration: "6 years",
            equityRange: "$80,000 - $120,000",
            lifeEvent: "Recently promoted",
            beds: 3, baths: 2, sqft: 1800, yearBuilt: 1995,
            lotSize: "0.25 acres",
            taxAssessment: 320_000m,
            annualTax: 8_500m,
            compCount: 6,
            searchRadius: "0.5 miles",
            valueRange: "$420,000 - $470,000",
            medianDom: 18,
            marketTrend: "Seller's",
            conversationStarters: ["Ask about equity", "Mention school demand"],
            leadEmail: "john@example.com",
            leadPhone: "555-0100",
            pdfLink: "[CMA PDF]"
        );

        content.Should().Contain("John Smith");
        content.Should().Contain("Software Engineer");
        content.Should().Contain("Google");
        content.Should().Contain("ASAP");
        content.Should().Contain("Conversation Starters");
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
cd apps/api && dotnet test --filter "GwsServiceTests"
```

Expected: FAIL.

**Step 3: Implement the service**

```csharp
// apps/api/RealEstateStar.Api/Services/Gws/IGwsService.cs
namespace RealEstateStar.Api.Services.Gws;

public interface IGwsService
{
    Task<string> CreateDriveFolderAsync(string agentEmail, string folderPath);
    Task<string> UploadFileAsync(string agentEmail, string folderPath, string filePath);
    Task<string> CreateDocAsync(string agentEmail, string folderPath, string title, string content);
    Task SendEmailAsync(string agentEmail, string to, string subject, string body, string? attachmentPath);
    Task AppendSheetRowAsync(string agentEmail, string spreadsheetId, List<string> values);
}
```

```csharp
// apps/api/RealEstateStar.Api/Services/Gws/GwsService.cs
using System.Diagnostics;
using System.Text;

namespace RealEstateStar.Api.Services.Gws;

public class GwsService : IGwsService
{
    public async Task<string> CreateDriveFolderAsync(string agentEmail, string folderPath)
    {
        return await RunGwsAsync($"drive mkdir --user {agentEmail} \"{folderPath}\"");
    }

    public async Task<string> UploadFileAsync(string agentEmail, string folderPath, string filePath)
    {
        return await RunGwsAsync($"drive upload --user {agentEmail} --parent \"{folderPath}\" \"{filePath}\"");
    }

    public async Task<string> CreateDocAsync(string agentEmail, string folderPath,
        string title, string content)
    {
        // Write content to temp file, then create doc from it
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, content);
        try
        {
            return await RunGwsAsync(
                $"docs create --user {agentEmail} --parent \"{folderPath}\" " +
                $"--title \"{title}\" --body-file \"{tempFile}\"");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    public async Task SendEmailAsync(string agentEmail, string to, string subject,
        string body, string? attachmentPath)
    {
        var cmd = $"gmail send --user {agentEmail} --to \"{to}\" " +
            $"--subject \"{subject}\" --body \"{body}\"";
        if (attachmentPath != null)
            cmd += $" --attachment \"{attachmentPath}\"";

        await RunGwsAsync(cmd);
    }

    public async Task AppendSheetRowAsync(string agentEmail, string spreadsheetId,
        List<string> values)
    {
        var csv = string.Join(",", values.Select(v => $"\"{v}\""));
        await RunGwsAsync(
            $"sheets append --user {agentEmail} --spreadsheet \"{spreadsheetId}\" --values {csv}");
    }

    private static async Task<string> RunGwsAsync(string args)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "gws",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new Exception($"gws command failed: {error}");
        }

        return output.Trim();
    }

    public static string BuildLeadFolderPath(string leadName, string address)
    {
        return $"Real Estate Star/1 - Leads/{leadName}/{address}";
    }

    public static string BuildLeadBriefContent(
        string leadName, string address, string timeline, DateTime submittedAt,
        string? occupation, string? employer, DateOnly? purchaseDate,
        decimal? purchasePrice, string? ownershipDuration, string? equityRange,
        string? lifeEvent, int? beds, int? baths, int? sqft, int? yearBuilt,
        string? lotSize, decimal? taxAssessment, decimal? annualTax,
        int compCount, string searchRadius, string valueRange, int medianDom,
        string marketTrend, List<string> conversationStarters,
        string leadEmail, string leadPhone, string pdfLink)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"New Lead Brief - {leadName}");
        sb.AppendLine(new string('=', 40));
        sb.AppendLine();
        sb.AppendLine($"Property: {address}");
        sb.AppendLine($"Timeline: {timeline}");
        sb.AppendLine($"Submitted: {submittedAt:yyyy-MM-dd} at {submittedAt:HH:mm}");
        sb.AppendLine();

        sb.AppendLine($"About {leadName.Split(' ')[0]}:");
        if (occupation != null) sb.AppendLine($"  {occupation} at {employer}");
        if (purchaseDate.HasValue && purchasePrice.HasValue)
            sb.AppendLine($"  Purchased {address.Split(',')[0]} in {purchaseDate:MMMM yyyy} for ${purchasePrice:N0}");
        if (ownershipDuration != null) sb.AppendLine($"  Owned for {ownershipDuration}");
        if (equityRange != null) sb.AppendLine($"  Estimated equity: {equityRange}");
        if (lifeEvent != null) sb.AppendLine($"  {lifeEvent}");
        sb.AppendLine();

        sb.AppendLine("Property Details (public records):");
        if (beds.HasValue) sb.AppendLine($"  {beds} bed / {baths} bath / {sqft:N0} sqft" +
            (yearBuilt.HasValue ? $", built {yearBuilt}" : ""));
        if (lotSize != null) sb.AppendLine($"  Lot: {lotSize}");
        if (taxAssessment.HasValue) sb.AppendLine($"  Current tax assessment: ${taxAssessment:N0}");
        if (annualTax.HasValue) sb.AppendLine($"  Annual property taxes: ${annualTax:N0}");
        sb.AppendLine();

        sb.AppendLine("Market Context:");
        sb.AppendLine($"  {compCount} comparable sales found in {searchRadius}");
        sb.AppendLine($"  Estimated current value: {valueRange}");
        sb.AppendLine($"  Median days on market: {medianDom}");
        sb.AppendLine($"  Market trending: {marketTrend} market");
        sb.AppendLine();

        sb.AppendLine("Conversation Starters:");
        foreach (var starter in conversationStarters)
        {
            sb.AppendLine($"  \"{starter}\"");
        }
        sb.AppendLine();

        sb.AppendLine($"CMA Status: Sent to {leadEmail}");
        sb.AppendLine($"CMA Report: {pdfLink}");
        sb.AppendLine();

        sb.AppendLine("Recommended Next Steps:");
        sb.AppendLine(timeline switch
        {
            "ASAP" => "  1. Call within 1 hour — this lead is ready NOW",
            "1-3 months" => "  1. Call within 2 hours — serious seller, time-sensitive",
            _ => "  1. Call within 24 hours — build the relationship early"
        });
        sb.AppendLine("  2. Reference their situation naturally in conversation");
        sb.AppendLine("  3. Schedule walkthrough");
        sb.AppendLine("  4. Prepare listing agreement");
        sb.AppendLine();

        sb.AppendLine("Contact:");
        sb.AppendLine($"  Phone: {leadPhone}");
        sb.AppendLine($"  Email: {leadEmail}");

        return sb.ToString();
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
cd apps/api && dotnet test --filter "GwsServiceTests"
```

Expected: Both tests PASS.

**Step 5: Commit**

```bash
git add apps/api/
git commit -m "feat(api): add gws service wrapper — Drive, Docs, Gmail, Sheets integration"
```

---

## Phase 8: Pipeline Orchestrator

### Task 12: CMA Pipeline Orchestrator

**Files:**
- Create: `apps/api/RealEstateStar.Api/Services/CmaPipeline.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Services/CmaPipelineTests.cs`

This is the heart of the system — it orchestrates all services in the correct order, handles errors, and pushes status updates.

**Step 1: Write failing tests**

```csharp
// apps/api/RealEstateStar.Api.Tests/Services/CmaPipelineTests.cs
using FluentAssertions;
using Moq;
using RealEstateStar.Api.Models;
using RealEstateStar.Api.Services;
using RealEstateStar.Api.Services.Analysis;
using RealEstateStar.Api.Services.Comps;
using RealEstateStar.Api.Services.Gws;
using RealEstateStar.Api.Services.Pdf;
using RealEstateStar.Api.Services.Research;

namespace RealEstateStar.Api.Tests.Services;

public class CmaPipelineTests
{
    private readonly Mock<IAgentConfigService> _agentConfig = new();
    private readonly Mock<CompAggregator> _compAggregator;
    private readonly Mock<ILeadResearchService> _research = new();
    private readonly Mock<IAnalysisService> _analysis = new();
    private readonly Mock<ICmaPdfGenerator> _pdf = new();
    private readonly Mock<IGwsService> _gws = new();

    public CmaPipelineTests()
    {
        _compAggregator = new Mock<CompAggregator>(new List<ICompSource>());
    }

    [Fact]
    public async Task Execute_CompletesAllSteps_ForValidInput()
    {
        var agent = new AgentConfig
        {
            Id = "test", Identity = new() { Name = "Test", Email = "test@test.com" }
        };
        _agentConfig.Setup(s => s.GetAgentAsync("test")).ReturnsAsync(agent);
        _analysis.Setup(s => s.AnalyzeAsync(
            It.IsAny<Lead>(), It.IsAny<List<Comp>>(),
            It.IsAny<LeadResearch?>(), It.IsAny<ReportType>()))
            .ReturnsAsync(new CmaAnalysis
            {
                ValueLow = 400_000, ValueMid = 425_000, ValueHigh = 450_000,
                MarketNarrative = "Strong", MarketTrend = "Seller's",
                MedianDaysOnMarket = 15, ConversationStarters = ["Test"]
            });

        var statusUpdates = new List<CmaJobStatus>();
        var pipeline = new CmaPipeline(
            _agentConfig.Object, _compAggregator.Object,
            _research.Object, _analysis.Object,
            _pdf.Object, _gws.Object);

        var lead = new Lead
        {
            FirstName = "John", LastName = "Smith",
            Email = "john@test.com", Phone = "555-0100",
            Address = "123 Main", City = "Old Bridge",
            State = "NJ", Zip = "08857", Timeline = "ASAP"
        };

        var job = await pipeline.ExecuteAsync("test", lead,
            status => statusUpdates.Add(status));

        job.Status.Should().Be(CmaJobStatus.Complete);
        statusUpdates.Should().Contain(CmaJobStatus.SearchingComps);
        statusUpdates.Should().Contain(CmaJobStatus.Analyzing);
        statusUpdates.Should().Contain(CmaJobStatus.Complete);
    }

    [Fact]
    public async Task Execute_ReturnsNull_ForUnknownAgent()
    {
        _agentConfig.Setup(s => s.GetAgentAsync("unknown")).ReturnsAsync((AgentConfig?)null);

        var pipeline = new CmaPipeline(
            _agentConfig.Object, _compAggregator.Object,
            _research.Object, _analysis.Object,
            _pdf.Object, _gws.Object);

        var lead = new Lead { FirstName = "Test", LastName = "Test" };

        var job = await pipeline.ExecuteAsync("unknown", lead, _ => { });

        job.Should().BeNull();
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
cd apps/api && dotnet test --filter "CmaPipelineTests"
```

Expected: FAIL.

**Step 3: Implement the orchestrator**

```csharp
// apps/api/RealEstateStar.Api/Services/CmaPipeline.cs
using RealEstateStar.Api.Models;
using RealEstateStar.Api.Services.Analysis;
using RealEstateStar.Api.Services.Comps;
using RealEstateStar.Api.Services.Gws;
using RealEstateStar.Api.Services.Pdf;
using RealEstateStar.Api.Services.Research;

namespace RealEstateStar.Api.Services;

public class CmaPipeline
{
    private readonly IAgentConfigService _agentConfig;
    private readonly CompAggregator _compAggregator;
    private readonly ILeadResearchService _research;
    private readonly IAnalysisService _analysis;
    private readonly ICmaPdfGenerator _pdf;
    private readonly IGwsService _gws;

    public CmaPipeline(
        IAgentConfigService agentConfig,
        CompAggregator compAggregator,
        ILeadResearchService research,
        IAnalysisService analysis,
        ICmaPdfGenerator pdf,
        IGwsService gws)
    {
        _agentConfig = agentConfig;
        _compAggregator = compAggregator;
        _research = research;
        _analysis = analysis;
        _pdf = pdf;
        _gws = gws;
    }

    public async Task<CmaJob?> ExecuteAsync(string agentId, Lead lead,
        Action<CmaJobStatus> onStatusChange)
    {
        // Step 1: Parse & validate
        var agent = await _agentConfig.GetAgentAsync(agentId);
        if (agent == null) return null;

        var job = CmaJob.Create(Guid.Parse(agentId.PadRight(32, '0').Substring(0, 32)
            .Replace("-", "").PadRight(32, '0')[..32]), lead);
        onStatusChange(CmaJobStatus.Parsing);

        // Step 2 & 3: Fetch comps and research lead IN PARALLEL
        job.AdvanceTo(CmaJobStatus.SearchingComps);
        onStatusChange(CmaJobStatus.SearchingComps);

        var compsTask = _compAggregator.FetchCompsAsync(
            lead.Address, lead.City, lead.State, lead.Zip,
            lead.Beds, lead.Baths, lead.Sqft);

        job.AdvanceTo(CmaJobStatus.ResearchingLead);
        onStatusChange(CmaJobStatus.ResearchingLead);

        var researchTask = _research.ResearchAsync(lead);

        await Task.WhenAll(compsTask, researchTask);
        job.Comps = await compsTask;
        job.LeadResearch = await researchTask;

        // Step 4: Claude API analysis
        job.AdvanceTo(CmaJobStatus.Analyzing);
        onStatusChange(CmaJobStatus.Analyzing);
        job.Analysis = await _analysis.AnalyzeAsync(
            lead, job.Comps, job.LeadResearch, job.ReportType);

        // Step 5: Generate PDF
        job.AdvanceTo(CmaJobStatus.GeneratingPdf);
        onStatusChange(CmaJobStatus.GeneratingPdf);
        var pdfDir = Path.Combine(Path.GetTempPath(), "cma");
        Directory.CreateDirectory(pdfDir);
        var pdfPath = Path.Combine(pdfDir,
            $"CMA_{lead.Address.Replace(" ", "_")}_{DateTime.Today:yyyy-MM-dd}.pdf");
        _pdf.Generate(pdfPath, agent, lead, job.Comps, job.Analysis,
            job.LeadResearch, job.ReportType);
        job.PdfPath = pdfPath;

        // Step 6: Organize Drive
        job.AdvanceTo(CmaJobStatus.OrganizingDrive);
        onStatusChange(CmaJobStatus.OrganizingDrive);
        try
        {
            var folderPath = GwsService.BuildLeadFolderPath(lead.FullName, lead.FullAddress);
            await _gws.CreateDriveFolderAsync(agent.Identity.Email, folderPath);
            await _gws.UploadFileAsync(agent.Identity.Email, folderPath, pdfPath);

            // Step 7: Create Lead Brief
            var briefContent = GwsService.BuildLeadBriefContent(
                leadName: lead.FullName,
                address: lead.FullAddress,
                timeline: lead.Timeline,
                submittedAt: DateTime.UtcNow,
                occupation: job.LeadResearch?.Occupation,
                employer: job.LeadResearch?.Employer,
                purchaseDate: job.LeadResearch?.PurchaseDate,
                purchasePrice: job.LeadResearch?.PurchasePrice,
                ownershipDuration: job.LeadResearch?.PurchaseDate != null
                    ? LeadResearchService.CalculateOwnershipDuration(job.LeadResearch.PurchaseDate.Value) : null,
                equityRange: job.LeadResearch?.EstimatedEquityLow != null
                    ? $"${job.LeadResearch.EstimatedEquityLow:N0} - ${job.LeadResearch.EstimatedEquityHigh:N0}" : null,
                lifeEvent: job.LeadResearch?.LifeEventInsight,
                beds: lead.Beds, baths: lead.Baths, sqft: lead.Sqft,
                yearBuilt: job.LeadResearch?.YearBuilt,
                lotSize: job.LeadResearch?.LotSize != null
                    ? $"{job.LeadResearch.LotSize} {job.LeadResearch.LotSizeUnit}" : null,
                taxAssessment: job.LeadResearch?.TaxAssessment,
                annualTax: job.LeadResearch?.AnnualPropertyTax,
                compCount: job.Comps.Count,
                searchRadius: "0.5 miles",
                valueRange: $"${job.Analysis.ValueLow:N0} - ${job.Analysis.ValueHigh:N0}",
                medianDom: job.Analysis.MedianDaysOnMarket,
                marketTrend: job.Analysis.MarketTrend,
                conversationStarters: job.Analysis.ConversationStarters,
                leadEmail: lead.Email,
                leadPhone: lead.Phone,
                pdfLink: "[Link to PDF in this folder]"
            );
            await _gws.CreateDocAsync(agent.Identity.Email, folderPath,
                $"Lead Brief - {lead.FullName}", briefContent);
        }
        catch { /* Drive failure doesn't block pipeline */ }

        // Step 8: Send email to lead
        job.AdvanceTo(CmaJobStatus.SendingEmail);
        onStatusChange(CmaJobStatus.SendingEmail);
        try
        {
            var emailBody = BuildEmailBody(agent, lead, job.Analysis);
            await _gws.SendEmailAsync(
                agent.Identity.Email,
                lead.Email,
                $"Your Home Value Report - {lead.FullAddress}",
                emailBody,
                pdfPath);
        }
        catch { /* Email failure queued for retry */ }

        // Step 9: Log to tracking sheet
        job.AdvanceTo(CmaJobStatus.Logging);
        onStatusChange(CmaJobStatus.Logging);
        try
        {
            await _gws.AppendSheetRowAsync(agent.Identity.Email, "lead-tracker",
            [
                DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"),
                lead.FullName,
                lead.Email,
                lead.Phone,
                lead.FullAddress,
                lead.Timeline,
                $"${job.Analysis.ValueMid:N0}",
                "CMA Sent"
            ]);
        }
        catch { /* Sheet failure doesn't block pipeline */ }

        // Done
        job.AdvanceTo(CmaJobStatus.Complete);
        job.CompletedAt = DateTime.UtcNow;
        onStatusChange(CmaJobStatus.Complete);

        return job;
    }

    private static string BuildEmailBody(AgentConfig agent, Lead lead, CmaAnalysis analysis)
    {
        return $"""
            Hi {lead.FirstName},

            Thank you for your interest in understanding your home's value. I've prepared a
            detailed Comparative Market Analysis for {lead.FullAddress}.

            Based on recent comparable sales in your area, I estimate your home's current
            value is in the range of ${analysis.ValueLow:N0} to ${analysis.ValueHigh:N0}.

            The local market is currently trending as a {analysis.MarketTrend} market, with
            homes selling in a median of {analysis.MedianDaysOnMarket} days.

            I've attached the full report for your review. I'd love to discuss these findings
            with you and answer any questions.

            Would you be available for a quick call this week?

            Warm regards,
            {agent.Identity.Name}, {agent.Identity.Title}
            {agent.Identity.Brokerage}
            {agent.Identity.Phone}
            {agent.Identity.Email}
            {agent.Identity.Website}
            {(agent.Identity.Languages.Count > 1 ? $"\nSe Habla Español" : "")}
            {agent.Identity.Tagline}
            """;
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
cd apps/api && dotnet test --filter "CmaPipelineTests"
```

Expected: Both tests PASS.

**Step 5: Commit**

```bash
git add apps/api/
git commit -m "feat(api): add CMA pipeline orchestrator — full 9-step flow with parallel execution"
```

---

## Phase 9: API Endpoints & WebSocket

### Task 13: CMA API Endpoints

**Files:**
- Modify: `apps/api/RealEstateStar.Api/Program.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Endpoints/CmaEndpointTests.cs`

**Step 1: Write failing integration tests**

```csharp
// apps/api/RealEstateStar.Api.Tests/Endpoints/CmaEndpointTests.cs
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RealEstateStar.Api.Tests.Endpoints;

public class CmaEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public CmaEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostCma_Returns202_WithJobId()
    {
        var response = await _client.PostAsJsonAsync(
            "/agents/jenise-buckalew/cma",
            new
            {
                firstName = "John",
                lastName = "Smith",
                email = "john@test.com",
                phone = "555-0100",
                address = "123 Main St",
                city = "Old Bridge",
                state = "NJ",
                zip = "08857",
                timeline = "ASAP"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await response.Content.ReadFromJsonAsync<CmaJobResponse>();
        body!.JobId.Should().NotBeEmpty();
        body.Status.Should().Be("processing");
    }

    [Fact]
    public async Task GetCmaStatus_Returns404_ForUnknownJob()
    {
        var response = await _client.GetAsync(
            $"/agents/jenise-buckalew/cma/{Guid.NewGuid()}/status");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

public record CmaJobResponse(string JobId, string Status);
```

**Step 2: Run tests to verify they fail**

```bash
cd apps/api && dotnet test --filter "CmaEndpointTests"
```

Expected: FAIL.

**Step 3: Implement the endpoints in Program.cs**

```csharp
// apps/api/RealEstateStar.Api/Program.cs
using System.Collections.Concurrent;
using RealEstateStar.Api.Models;
using RealEstateStar.Api.Services;
using RealEstateStar.Api.Services.Analysis;
using RealEstateStar.Api.Services.Comps;
using RealEstateStar.Api.Services.Gws;
using RealEstateStar.Api.Services.Pdf;
using RealEstateStar.Api.Services.Research;

var builder = WebApplication.CreateBuilder(args);

// Register services
var configDir = Path.Combine(builder.Environment.ContentRootPath, "..", "..", "..", "config", "agents");
builder.Services.AddSingleton<IAgentConfigService>(new AgentConfigService(configDir));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ILeadResearchService, LeadResearchService>();
builder.Services.AddSingleton<ICmaPdfGenerator, CmaPdfGenerator>();
builder.Services.AddSingleton<IGwsService, GwsService>();

builder.Services.AddSingleton<IAnalysisService>(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var apiKey = builder.Configuration["Anthropic:ApiKey"] ?? "";
    return new ClaudeAnalysisService(http, apiKey);
});

builder.Services.AddSingleton(sp =>
{
    var sources = sp.GetServices<ICompSource>().ToList();
    return new CompAggregator(sources);
});

builder.Services.AddSingleton<CmaPipeline>();
builder.Services.AddSignalR();

var app = builder.Build();

// In-memory job store (replace with DB later)
var jobs = new ConcurrentDictionary<string, CmaJob>();

// POST /agents/{id}/cma
app.MapPost("/agents/{agentId}/cma", async (
    string agentId,
    Lead lead,
    CmaPipeline pipeline) =>
{
    var job = CmaJob.Create(Guid.NewGuid(), lead);
    jobs[job.Id.ToString()] = job;

    // Fire and forget — pipeline runs in background
    _ = Task.Run(async () =>
    {
        var result = await pipeline.ExecuteAsync(agentId, lead,
            status => { if (jobs.TryGetValue(job.Id.ToString(), out var j)) j.AdvanceTo(status); });
        if (result != null)
        {
            jobs[job.Id.ToString()] = result;
        }
    });

    return Results.Accepted(
        $"/agents/{agentId}/cma/{job.Id}/status",
        new { jobId = job.Id.ToString(), status = "processing" });
});

// GET /agents/{id}/cma/{jobId}/status
app.MapGet("/agents/{agentId}/cma/{jobId}/status", (string agentId, string jobId) =>
{
    if (!jobs.TryGetValue(jobId, out var job))
        return Results.NotFound();

    return Results.Ok(new
    {
        status = job.Status.ToString().ToLowerInvariant(),
        step = job.Step,
        totalSteps = job.TotalSteps,
        message = GetStatusMessage(job.Status)
    });
});

// GET /agents/{id}/leads
app.MapGet("/agents/{agentId}/leads", (string agentId) =>
{
    var agentLeads = jobs.Values
        .Where(j => j.AgentId.ToString().StartsWith(agentId))
        .Select(j => new
        {
            id = j.Id,
            name = j.Lead.FullName,
            address = j.Lead.FullAddress,
            timeline = j.Lead.Timeline,
            cmaStatus = j.Status.ToString(),
            submittedAt = j.CreatedAt,
            driveLink = j.DriveLink
        })
        .OrderByDescending(j => j.submittedAt);

    return Results.Ok(agentLeads);
});

app.Run();

static string GetStatusMessage(CmaJobStatus status) => status switch
{
    CmaJobStatus.Parsing => "Received your property details",
    CmaJobStatus.SearchingComps => "Searching MLS databases...",
    CmaJobStatus.ResearchingLead => "Researching property records...",
    CmaJobStatus.Analyzing => "Analyzing market trends...",
    CmaJobStatus.GeneratingPdf => "Generating your personalized report...",
    CmaJobStatus.OrganizingDrive => "Organizing documents...",
    CmaJobStatus.SendingEmail => "Sending report to your email...",
    CmaJobStatus.Logging => "Finalizing...",
    CmaJobStatus.Complete => "Your report has been sent to your email!",
    _ => "Processing..."
};

// Make Program accessible for integration tests
public partial class Program { }
```

**Step 4: Run tests to verify they pass**

```bash
cd apps/api && dotnet test --filter "CmaEndpointTests"
```

Expected: Both tests PASS.

**Step 5: Commit**

```bash
git add apps/api/
git commit -m "feat(api): add CMA endpoints — POST /agents/{id}/cma, GET status, GET leads"
```

---

### Task 14: WebSocket Hub for Real-Time Status

**Files:**
- Create: `apps/api/RealEstateStar.Api/Hubs/CmaProgressHub.cs`
- Modify: `apps/api/RealEstateStar.Api/Program.cs` (add SignalR hub mapping)

**Step 1: Create the SignalR hub**

```csharp
// apps/api/RealEstateStar.Api/Hubs/CmaProgressHub.cs
using Microsoft.AspNetCore.SignalR;

namespace RealEstateStar.Api.Hubs;

public class CmaProgressHub : Hub
{
    public async Task JoinJob(string jobId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, jobId);
    }
}
```

**Step 2: Wire it into Program.cs**

Add to `Program.cs` after `builder.Services.AddSignalR();`:

```csharp
// After app = builder.Build():
app.MapHub<CmaProgressHub>("/hubs/cma-progress");
```

Update the pipeline execution in POST endpoint to push status via SignalR:

```csharp
// In the Task.Run block, inject IHubContext<CmaProgressHub>
var hubContext = app.Services.GetRequiredService<IHubContext<CmaProgressHub>>();
// In the onStatusChange callback:
status =>
{
    if (jobs.TryGetValue(job.Id.ToString(), out var j)) j.AdvanceTo(status);
    hubContext.Clients.Group(job.Id.ToString())
        .SendAsync("StatusUpdate", new
        {
            status = status.ToString().ToLowerInvariant(),
            step = (int)status + 1,
            totalSteps = 9,
            message = GetStatusMessage(status)
        });
}
```

**Step 3: Verify it builds**

```bash
cd apps/api && dotnet build
```

Expected: Build succeeds.

**Step 4: Commit**

```bash
git add apps/api/
git commit -m "feat(api): add SignalR hub for real-time CMA progress updates"
```

---

## Phase 10: End-to-End Verification

### Task 15: Integration Test — Full Pipeline

**Files:**
- Create: `apps/api/RealEstateStar.Api.Tests/Integration/CmaPipelineIntegrationTests.cs`

**Step 1: Write integration test with mocked external services**

```csharp
// apps/api/RealEstateStar.Api.Tests/Integration/CmaPipelineIntegrationTests.cs
using FluentAssertions;
using Moq;
using RealEstateStar.Api.Models;
using RealEstateStar.Api.Services;
using RealEstateStar.Api.Services.Analysis;
using RealEstateStar.Api.Services.Comps;
using RealEstateStar.Api.Services.Gws;
using RealEstateStar.Api.Services.Pdf;
using RealEstateStar.Api.Services.Research;

namespace RealEstateStar.Api.Tests.Integration;

public class CmaPipelineIntegrationTests
{
    [Fact]
    public async Task FullPipeline_GeneratesPdf_AndCompletes()
    {
        // Arrange — real config loader, real PDF generator, mocked externals
        var agentConfig = new AgentConfigService(
            Path.Combine(Directory.GetCurrentDirectory(),
                "..", "..", "..", "..", "..", "..", "config", "agents"));

        var mockCompSource = new Mock<ICompSource>();
        mockCompSource.Setup(s => s.FetchAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<Comp>
            {
                new() { Address = "100 Oak Ave, Old Bridge, NJ 08857",
                    SalePrice = 450_000, SaleDate = new DateOnly(2026, 1, 15),
                    Beds = 3, Baths = 2, Sqft = 1800,
                    DaysOnMarket = 15, Source = CompSource.Mls },
                new() { Address = "200 Elm St, Old Bridge, NJ 08857",
                    SalePrice = 430_000, SaleDate = new DateOnly(2026, 2, 1),
                    Beds = 3, Baths = 2, Sqft = 1750,
                    DaysOnMarket = 22, Source = CompSource.Zillow },
            });

        var aggregator = new CompAggregator([mockCompSource.Object]);

        var mockResearch = new Mock<ILeadResearchService>();
        mockResearch.Setup(s => s.ResearchAsync(It.IsAny<Lead>()))
            .ReturnsAsync(new LeadResearch
            {
                Occupation = "Software Engineer",
                Employer = "Google",
                PurchaseDate = new DateOnly(2019, 5, 1),
                PurchasePrice = 300_000
            });

        var mockAnalysis = new Mock<IAnalysisService>();
        mockAnalysis.Setup(s => s.AnalyzeAsync(
            It.IsAny<Lead>(), It.IsAny<List<Comp>>(),
            It.IsAny<LeadResearch?>(), It.IsAny<ReportType>()))
            .ReturnsAsync(new CmaAnalysis
            {
                ValueLow = 420_000, ValueMid = 445_000, ValueHigh = 470_000,
                MarketNarrative = "Old Bridge continues to see strong demand.",
                PricingRecommendation = "List at $449,900 to generate multiple offers.",
                LeadInsights = "Owned for 7 years with significant equity.",
                ConversationStarters = [
                    "Your equity has grown significantly since 2019",
                    "The seller's market means strong offers",
                    "Old Bridge schools continue to drive demand"
                ],
                MarketTrend = "Seller's",
                MedianDaysOnMarket = 18
            });

        var pdfGenerator = new CmaPdfGenerator();
        var mockGws = new Mock<IGwsService>();

        var pipeline = new CmaPipeline(
            agentConfig, aggregator, mockResearch.Object,
            mockAnalysis.Object, pdfGenerator, mockGws.Object);

        var statusLog = new List<CmaJobStatus>();

        // Act
        var lead = new Lead
        {
            FirstName = "John", LastName = "Smith",
            Email = "john@test.com", Phone = "555-0100",
            Address = "123 Main St", City = "Old Bridge",
            State = "NJ", Zip = "08857",
            Timeline = "ASAP", Beds = 3, Baths = 2, Sqft = 1800
        };

        var job = await pipeline.ExecuteAsync("jenise-buckalew", lead,
            status => statusLog.Add(status));

        // Assert
        job.Should().NotBeNull();
        job!.Status.Should().Be(CmaJobStatus.Complete);
        job.Comps.Should().HaveCount(2);
        job.Analysis.Should().NotBeNull();
        job.PdfPath.Should().NotBeNull();
        File.Exists(job.PdfPath).Should().BeTrue();
        job.ReportType.Should().Be(ReportType.Comprehensive);

        statusLog.Should().ContainInOrder(
            CmaJobStatus.Parsing,
            CmaJobStatus.SearchingComps,
            CmaJobStatus.ResearchingLead,
            CmaJobStatus.Analyzing,
            CmaJobStatus.GeneratingPdf,
            CmaJobStatus.OrganizingDrive,
            CmaJobStatus.SendingEmail,
            CmaJobStatus.Logging,
            CmaJobStatus.Complete);

        // Verify gws was called
        mockGws.Verify(g => g.CreateDriveFolderAsync(
            "jenisesellsnj@gmail.com", It.IsAny<string>()), Times.Once);
        mockGws.Verify(g => g.SendEmailAsync(
            "jenisesellsnj@gmail.com", "john@test.com",
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);

        // Cleanup
        if (File.Exists(job.PdfPath)) File.Delete(job.PdfPath);
    }
}
```

**Step 2: Run integration test**

```bash
cd apps/api && dotnet test --filter "CmaPipelineIntegrationTests"
```

Expected: PASS — full pipeline executes with mocked externals, real PDF generated.

**Step 3: Commit**

```bash
git add apps/api/
git commit -m "test(api): add full CMA pipeline integration test"
```

---

### Task 16: Update SKILL.md and CLAUDE.md

**Files:**
- Modify: `skills/cma/SKILL.md` — update to reference .NET API instead of Python/reportlab
- Modify: `.claude/CLAUDE.md` — add CMA pipeline docs reference

**Step 1: Update CMA SKILL.md**

Replace references to Python/reportlab with .NET/QuestPDF. Update the workflow to describe the API-driven pipeline rather than the Claude Code skill-based flow. Keep the agent config loading, error handling, and state-specific notes.

**Step 2: Update CLAUDE.md**

Add under Docs section:
```
- CMA Pipeline Design: `docs/plans/2026-03-09-cma-pipeline-design.md`
- CMA Pipeline Plan: `docs/plans/2026-03-09-cma-pipeline-plan.md`
```

**Step 3: Commit**

```bash
git add skills/cma/SKILL.md .claude/CLAUDE.md
git commit -m "docs: update CMA skill and CLAUDE.md for .NET pipeline architecture"
```

---

## Summary

| Phase | Tasks | What It Builds |
|-------|-------|---------------|
| 1: Scaffold | 1-3 | .NET 10 API project, test project, domain models |
| 2: Config | 4 | Agent config loader (reads JSON from config/agents/) |
| 3: Comps | 5-7 | Comp aggregator with parallel fetch, dedup, 4 sources |
| 4: Research | 8 | Lead research from public records, LinkedIn, neighborhood |
| 5: Analysis | 9 | Claude API prompt builder + JSON response parser |
| 6: PDF | 10 | QuestPDF generator with adaptive lean/standard/comprehensive |
| 7: GWS | 11 | Google Workspace CLI wrapper (Drive, Docs, Gmail, Sheets) |
| 8: Pipeline | 12 | Orchestrator — 9-step flow with parallel execution |
| 9: API | 13-14 | REST endpoints + SignalR WebSocket hub |
| 10: Verify | 15-16 | Integration test + docs update |

**Total: 16 tasks across 10 phases**
