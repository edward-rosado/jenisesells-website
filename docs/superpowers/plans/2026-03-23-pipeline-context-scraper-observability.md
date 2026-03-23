# Pipeline Context Refactor + ScraperAPI Observability

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Introduce a unified `IPipelineContext<T>` that accumulates data across pipeline steps (eliminating redundant scraping), add ScraperAPI usage observability with configurable URLs and rate-limit handling.

**Architecture:** Each pipeline gets a context object that carries the original request plus all intermediate results. Steps check the context for existing data before calling external APIs. ScraperAPI calls go through a centralized `IScraperClient` with OTel counters, configurable base URLs, and circuit-breaker behavior when limits are reached.

**Tech Stack:** .NET 10, System.Threading.Channels, OpenTelemetry Metrics, IOptions pattern

---

## File Map

### New Files

| File | Responsibility |
|------|---------------|
| `Workers.Shared/Context/PipelineContext.cs` | Base `PipelineContext<TRequest>` with `Data` dictionary + status |
| `Workers.Shared/Context/IPipelineStep.cs` | `IPipelineStep<TContext>` interface for composable steps |
| `Workers.Leads/LeadPipelineContext.cs` | Lead-specific context with typed accessors for enrichment, draft, etc. |
| `Workers.Cma/CmaPipelineContext.cs` | CMA-specific context with typed accessors for comps, analysis, PDF |
| `Workers.HomeSearch/HomeSearchPipelineContext.cs` | HomeSearch context with typed accessors for listings |
| `Clients.Scraper/ScraperClient.cs` | Centralized scraper with OTel, config, rate-limit handling |
| `Clients.Scraper/ScraperOptions.cs` | Config: base URL, API key, rate limit thresholds |
| `Clients.Scraper/ScraperDiagnostics.cs` | OTel counters for scraper calls |
| `Domain/Shared/Interfaces/External/IScraperClient.cs` | Interface in Domain |

### Modified Files

| File | Change |
|------|--------|
| `Workers.Leads/LeadProcessingWorker.cs` | Use `LeadPipelineContext` instead of raw Lead + tuple returns |
| `Workers.Cma/CmaProcessingWorker.cs` | Use `CmaPipelineContext` |
| `Workers.HomeSearch/HomeSearchProcessingWorker.cs` | Use `HomeSearchPipelineContext` |
| `Workers.Leads/ScraperLeadEnricher.cs` | Use `IScraperClient` instead of raw HttpClient |
| `Workers.Cma/ScraperCompSource.cs` | Use `IScraperClient` instead of raw HttpClient |
| `Workers.HomeSearch/ScraperHomeSearchProvider.cs` | Use `IScraperClient` instead of raw HttpClient |
| `Api/Program.cs` | Register `IScraperClient`, `ScraperOptions` |
| `Api/appsettings.json` | Add `Scraper` config section |
| Channel request records | Add context field or replace with context |

---

## Phase 1: ScraperAPI Observability + Config (Tasks 1-4)

### Task 1: IScraperClient interface + ScraperOptions config

**Files:**
- Create: `apps/api/RealEstateStar.Domain/Shared/Interfaces/External/IScraperClient.cs`
- Create: `apps/api/RealEstateStar.Clients.Scraper/ScraperOptions.cs`

- [ ] **Step 1: Write IScraperClient interface**

```csharp
// Domain/Shared/Interfaces/External/IScraperClient.cs
namespace RealEstateStar.Domain.Shared.Interfaces.External;

public interface IScraperClient
{
    Task<string?> FetchAsync(string targetUrl, string source, string agentId, CancellationToken ct);
    bool IsAvailable { get; }
}
```

- [ ] **Step 2: Write ScraperOptions**

```csharp
// Clients.Scraper/ScraperOptions.cs
namespace RealEstateStar.Clients.Scraper;

public class ScraperOptions
{
    public string ApiKey { get; init; } = "";
    public string BaseUrl { get; init; } = "https://api.scraperapi.com";
    public bool RenderJavaScript { get; init; } = true;
    public int TimeoutSeconds { get; init; } = 30;
    public int MonthlyLimitWarningPercent { get; init; } = 70;
    public int MaxRetriesOnRateLimit { get; init; } = 1;
    public Dictionary<string, string> SourceUrls { get; init; } = new()
    {
        ["zillow"] = "https://www.zillow.com/homedetails/{slug}",
        ["redfin"] = "https://www.redfin.com/home/{slug}",
        ["realtor"] = "https://www.realtor.com/realestateandhomes-detail/{slug}",
        ["google"] = "https://www.google.com/search?q={query}",
    };
}
```

- [ ] **Step 3: Add config to appsettings.json**

```json
{
  "Scraper": {
    "BaseUrl": "https://api.scraperapi.com",
    "RenderJavaScript": true,
    "TimeoutSeconds": 30,
    "MonthlyLimitWarningPercent": 70,
    "SourceUrls": {
      "zillow": "https://www.zillow.com/homedetails/{slug}",
      "redfin": "https://www.redfin.com/home/{slug}",
      "realtor": "https://www.realtor.com/realestateandhomes-detail/{slug}"
    }
  }
}
```

- [ ] **Step 4: Commit**

```
feat: add IScraperClient interface and ScraperOptions config
```

---

### Task 2: ScraperDiagnostics OTel counters

**Files:**
- Create: `apps/api/RealEstateStar.Clients.Scraper/ScraperDiagnostics.cs`
- Modify: `apps/api/RealEstateStar.Api/Diagnostics/OpenTelemetryExtensions.cs` (add meter)

- [ ] **Step 1: Write ScraperDiagnostics**

```csharp
// Clients.Scraper/ScraperDiagnostics.cs
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RealEstateStar.Clients.Scraper;

public static class ScraperDiagnostics
{
    public const string ServiceName = "RealEstateStar.Scraper";
    public static readonly ActivitySource ActivitySource = new(ServiceName, "1.0.0");
    private static readonly Meter Meter = new(ServiceName, "1.0.0");

    // Counters
    public static readonly Counter<long> CallsTotal = Meter.CreateCounter<long>("scraper.calls_total");
    public static readonly Counter<long> CallsSucceeded = Meter.CreateCounter<long>("scraper.calls_succeeded");
    public static readonly Counter<long> CallsFailed = Meter.CreateCounter<long>("scraper.calls_failed");
    public static readonly Counter<long> CallsRateLimited = Meter.CreateCounter<long>("scraper.calls_rate_limited");
    public static readonly Counter<long> CreditsUsed = Meter.CreateCounter<long>("scraper.credits_used");

    // Histograms
    public static readonly Histogram<double> CallDuration = Meter.CreateHistogram<double>("scraper.call_duration_ms");

    // Observable — tracks if scraper is currently available
    public static bool Available { get; set; } = true;
}
```

- [ ] **Step 2: Register meter in OTel extensions**

Add `.AddMeter(ScraperDiagnostics.ServiceName)` to both tracing and metrics in `OpenTelemetryExtensions.cs`.

- [ ] **Step 3: Commit**

```
feat: add ScraperDiagnostics OTel counters for usage monitoring
```

---

### Task 3: ScraperClient implementation with rate-limit handling

**Files:**
- Create: `apps/api/RealEstateStar.Clients.Scraper/ScraperClient.cs`
- Test: `apps/api/tests/RealEstateStar.Clients.Scraper.Tests/ScraperClientTests.cs`

- [ ] **Step 1: Write failing tests**

Tests for:
- `FetchAsync_ReturnsHtml_WhenSuccessful`
- `FetchAsync_IncrementsCallsTotal_Counter`
- `FetchAsync_ReturnsNull_WhenRateLimited` (HTTP 429)
- `FetchAsync_SetsIsAvailableFalse_WhenRateLimited`
- `FetchAsync_ReturnsNull_WhenTimeout`
- `FetchAsync_LogsSourceAndAgentId`
- `FetchAsync_UsesConfiguredBaseUrl`

- [ ] **Step 2: Implement ScraperClient**

```csharp
// Clients.Scraper/ScraperClient.cs
public class ScraperClient(
    IHttpClientFactory httpClientFactory,
    IOptions<ScraperOptions> options,
    ILogger<ScraperClient> logger) : IScraperClient
{
    private volatile bool _available = true;
    public bool IsAvailable => _available;

    public async Task<string?> FetchAsync(string targetUrl, string source, string agentId, CancellationToken ct)
    {
        if (!_available)
        {
            logger.LogWarning("[SCRAPER-010] Scraper unavailable (rate limited). Skipping {Source} for agent {AgentId}", source, agentId);
            return null;
        }

        var opts = options.Value;
        var url = $"{opts.BaseUrl}?api_key={opts.ApiKey}&url={Uri.EscapeDataString(targetUrl)}" +
                  (opts.RenderJavaScript ? "&render=true" : "");

        using var activity = ScraperDiagnostics.ActivitySource.StartActivity("scraper.fetch");
        activity?.SetTag("scraper.source", source);
        activity?.SetTag("scraper.agent_id", agentId);

        var sw = Stopwatch.GetTimestamp();
        ScraperDiagnostics.CallsTotal.Add(1, new KeyValuePair<string, object?>("source", source));

        try
        {
            var client = httpClientFactory.CreateClient("ScraperAPI");
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);

            var response = await client.GetAsync(url, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _available = false;
                ScraperDiagnostics.CallsRateLimited.Add(1, new KeyValuePair<string, object?>("source", source));
                ScraperDiagnostics.Available = false;
                logger.LogError("[SCRAPER-020] ScraperAPI rate limit reached. Disabling scraper. Source: {Source}, Agent: {AgentId}", source, agentId);
                return null;
            }

            response.EnsureSuccessStatusCode();

            ScraperDiagnostics.CallsSucceeded.Add(1, new KeyValuePair<string, object?>("source", source));
            ScraperDiagnostics.CreditsUsed.Add(opts.RenderJavaScript ? 10 : 1);

            var html = await response.Content.ReadAsStringAsync(ct);
            logger.LogInformation("[SCRAPER-001] Fetched {Source} for agent {AgentId}. Size: {Size}KB. Duration: {Duration}ms",
                source, agentId, html.Length / 1024, Stopwatch.GetElapsedTime(sw).TotalMilliseconds);

            return html;
        }
        catch (TaskCanceledException)
        {
            ScraperDiagnostics.CallsFailed.Add(1, new KeyValuePair<string, object?>("source", source));
            logger.LogWarning("[SCRAPER-030] Timeout fetching {Source} for agent {AgentId}", source, agentId);
            return null;
        }
        catch (Exception ex)
        {
            ScraperDiagnostics.CallsFailed.Add(1, new KeyValuePair<string, object?>("source", source));
            logger.LogError(ex, "[SCRAPER-040] Failed fetching {Source} for agent {AgentId}", source, agentId);
            return null;
        }
        finally
        {
            ScraperDiagnostics.CallDuration.Record(Stopwatch.GetElapsedTime(sw).TotalMilliseconds,
                new KeyValuePair<string, object?>("source", source));
        }
    }
}
```

- [ ] **Step 3: Run tests, verify pass**
- [ ] **Step 4: Commit**

```
feat: implement ScraperClient with OTel, rate-limit circuit breaker, configurable URLs
```

---

### Task 4: Wire ScraperClient into DI + migrate existing scraper calls

**Files:**
- Modify: `apps/api/RealEstateStar.Api/Program.cs`
- Modify: `apps/api/RealEstateStar.Workers.Leads/ScraperLeadEnricher.cs`
- Modify: `apps/api/RealEstateStar.Workers.Cma/ScraperCompSource.cs`
- Modify: `apps/api/RealEstateStar.Workers.HomeSearch/ScraperHomeSearchProvider.cs`

- [ ] **Step 1: Register in Program.cs**

```csharp
builder.Services.Configure<ScraperOptions>(builder.Configuration.GetSection("Scraper"));
builder.Services.AddSingleton<IScraperClient, ScraperClient>();
```

- [ ] **Step 2: Migrate ScraperLeadEnricher to use IScraperClient**

Replace raw `httpClientFactory.CreateClient("ScraperAPI")` + manual URL construction with `scraperClient.FetchAsync(targetUrl, "google-search", agentId, ct)`. Check `scraperClient.IsAvailable` before starting batch.

- [ ] **Step 3: Migrate ScraperCompSource to use IScraperClient**

Replace `BuildScraperUrl()` + raw fetch with `scraperClient.FetchAsync(targetUrl, sourceName, agentId, ct)`.

- [ ] **Step 4: Migrate ScraperHomeSearchProvider to use IScraperClient**

Replace raw fetch with `scraperClient.FetchAsync(targetUrl, "home-search-{source}", agentId, ct)`.

- [ ] **Step 5: Run all tests, verify pass**
- [ ] **Step 6: Commit**

```
refactor: migrate all scraper calls to centralized IScraperClient
```

---

## Architecture Decision: Fan-Out at Endpoint

The three pipelines (Lead Enrichment, CMA, Home Search) are **independent**:

```
API Endpoint (validate + save + consent)
  │
  ├─ LeadProcessingChannel  → enrichment + notification
  ├─ CmaProcessingChannel   → comps + Claude analysis + PDF
  └─ HomeSearchChannel      → listings + notification
```

Currently the endpoint chains: endpoint → lead worker → (CMA, HomeSearch). But CMA only uses `LeadScore` for report-type selection (can default), and `enrichment` is destructured but never used in CMA. Home search has zero dependency on enrichment.

**New flow:** The endpoint fans out to ALL 3 channels immediately. Each worker runs independently with its own context. CMA gets `LeadScore.Default()` and can upgrade later if enrichment finishes first (via checkpoint file).

**CmaProcessingRequest changes:** Remove `LeadEnrichment` and `LeadScore` params. The CMA worker defaults to `LeadScore.Default()` for `DetermineReportType`. If enrichment completes first and writes `Research & Insights.md`, CMA can optionally read it.

---

## Phase 2: Pipeline Context Pattern (Tasks 5-8)

### Task 5: PipelineContext base + IPipelineStep

**Files:**
- Create: `apps/api/RealEstateStar.Workers.Shared/Context/PipelineContext.cs`
- Create: `apps/api/RealEstateStar.Workers.Shared/Context/PipelineStepStatus.cs`

- [ ] **Step 1: Write PipelineContext**

```csharp
// Workers.Shared/Context/PipelineContext.cs
namespace RealEstateStar.Workers.Shared.Context;

public class PipelineContext<TRequest>
{
    public required TRequest Request { get; init; }
    public required string AgentId { get; init; }
    public required string CorrelationId { get; init; }
    public Dictionary<string, object> Data { get; } = new();
    public Dictionary<string, PipelineStepStatus> StepResults { get; } = new();

    public T? Get<T>(string key) where T : class =>
        Data.TryGetValue(key, out var value) ? value as T : null;

    public void Set<T>(string key, T value) where T : class =>
        Data[key] = value;

    public bool HasCompleted(string stepName) =>
        StepResults.TryGetValue(stepName, out var status) && status == PipelineStepStatus.Completed;

    public void MarkCompleted(string stepName) =>
        StepResults[stepName] = PipelineStepStatus.Completed;

    public void MarkFailed(string stepName) =>
        StepResults[stepName] = PipelineStepStatus.Failed;

    public void MarkSkipped(string stepName) =>
        StepResults[stepName] = PipelineStepStatus.Skipped;
}
```

```csharp
// Workers.Shared/Context/PipelineStepStatus.cs
namespace RealEstateStar.Workers.Shared.Context;

public enum PipelineStepStatus { Pending, Completed, Failed, Skipped }
```

- [ ] **Step 2: Commit**

```
feat: add PipelineContext<T> base class with step tracking
```

---

### Task 6: LeadPipelineContext with typed accessors

**Files:**
- Create: `apps/api/RealEstateStar.Workers.Leads/LeadPipelineContext.cs`

- [ ] **Step 1: Write LeadPipelineContext**

```csharp
namespace RealEstateStar.Workers.Leads;

using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Workers.Shared.Context;

public class LeadPipelineContext : PipelineContext<Lead>
{
    // Step names
    public const string StepEnrich = "enrich";
    public const string StepDraftEmail = "draft-email";
    public const string StepNotify = "notify";
    public const string StepDispatchCma = "dispatch-cma";
    public const string StepDispatchHomeSearch = "dispatch-home-search";

    // Typed accessors for intermediate results
    public LeadEnrichment? Enrichment
    {
        get => Get<LeadEnrichment>("enrichment");
        set { if (value is not null) Set("enrichment", value); }
    }

    public LeadScore? Score
    {
        get => Get<LeadScore>("score");
        set { if (value is not null) Set("score", value); }
    }

    public string? EmailDraftSubject
    {
        get => Data.TryGetValue("email-draft-subject", out var v) ? v as string : null;
        set { if (value is not null) Data["email-draft-subject"] = value; }
    }

    public string? EmailDraftBody
    {
        get => Data.TryGetValue("email-draft-body", out var v) ? v as string : null;
        set { if (value is not null) Data["email-draft-body"] = value; }
    }
}
```

- [ ] **Step 2: Commit**

```
feat: add LeadPipelineContext with typed accessors for enrichment, draft, score
```

---

### Task 7: CmaPipelineContext + HomeSearchPipelineContext

**Files:**
- Create: `apps/api/RealEstateStar.Workers.Cma/CmaPipelineContext.cs`
- Create: `apps/api/RealEstateStar.Workers.HomeSearch/HomeSearchPipelineContext.cs`

- [ ] **Step 1: Write CmaPipelineContext**

```csharp
namespace RealEstateStar.Workers.Cma;

using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Workers.Shared.Context;

public class CmaPipelineContext : PipelineContext<Lead>
{
    public const string StepFetchComps = "fetch-comps";
    public const string StepAnalyze = "analyze";
    public const string StepGeneratePdf = "generate-pdf";
    public const string StepNotifySeller = "notify-seller";

    public LeadEnrichment? Enrichment
    {
        get => Get<LeadEnrichment>("enrichment");
        set { if (value is not null) Set("enrichment", value); }
    }

    public LeadScore? Score
    {
        get => Get<LeadScore>("score");
        set { if (value is not null) Set("score", value); }
    }

    public List<Comp>? Comps
    {
        get => Get<List<Comp>>("comps");
        set { if (value is not null) Set("comps", value); }
    }

    public CmaAnalysis? Analysis
    {
        get => Get<CmaAnalysis>("analysis");
        set { if (value is not null) Set("analysis", value); }
    }

    public byte[]? PdfBytes
    {
        get => Get<byte[]>("pdf-bytes");
        set { if (value is not null) Set("pdf-bytes", value); }
    }
}
```

- [ ] **Step 2: Write HomeSearchPipelineContext** (similar pattern)

- [ ] **Step 3: Commit**

```
feat: add CmaPipelineContext and HomeSearchPipelineContext
```

---

### Task 8: Migrate LeadProcessingWorker to use LeadPipelineContext

**Files:**
- Modify: `apps/api/RealEstateStar.Workers.Leads/LeadProcessingWorker.cs`
- Modify: `apps/api/RealEstateStar.Workers.Leads/LeadProcessingChannel.cs`
- Modify: test files

- [ ] **Step 1: Update LeadProcessingRequest to carry context**

```csharp
public sealed record LeadProcessingRequest(
    string AgentId,
    Lead Lead,
    string CorrelationId,
    LeadPipelineContext? Context = null);
```

- [ ] **Step 2: Refactor ProcessLeadAsync to build and pass context**

```csharp
private async Task ProcessLeadAsync(LeadProcessingRequest request, CancellationToken ct)
{
    var ctx = request.Context ?? new LeadPipelineContext
    {
        Request = request.Lead,
        AgentId = request.AgentId,
        CorrelationId = request.CorrelationId,
    };

    // Step 1: Enrich — skip if context already has enrichment
    if (!ctx.HasCompleted(LeadPipelineContext.StepEnrich))
        await EnrichLeadAsync(ctx, ct);

    // Step 2: Draft + send notification
    if (!ctx.HasCompleted(LeadPipelineContext.StepDraftEmail))
        await DraftEmailAsync(ctx, ct);

    var notifyTask = NotifyAgentAsync(ctx, ct);

    // Step 3-4: Dispatch CMA/HomeSearch in parallel
    if (!ctx.HasCompleted(LeadPipelineContext.StepDispatchCma))
        await DispatchCmaAsync(ctx, ct);
    if (!ctx.HasCompleted(LeadPipelineContext.StepDispatchHomeSearch))
        await DispatchHomeSearchAsync(ctx, ct);

    await notifyTask;
}
```

Each step method updates `ctx.Enrichment`, `ctx.Score`, etc. instead of returning tuples.

- [ ] **Step 3: Update tests**
- [ ] **Step 4: Run all tests, verify pass**
- [ ] **Step 5: Commit**

```
refactor: migrate LeadProcessingWorker to LeadPipelineContext
```

---

### Task 9: Migrate CmaProcessingWorker to CmaPipelineContext

**Files:**
- Modify: `apps/api/RealEstateStar.Workers.Cma/CmaProcessingWorker.cs`
- Modify: `apps/api/RealEstateStar.Workers.Cma/CmaProcessingChannel.cs`

- [ ] **Step 1: Update CmaProcessingRequest**

```csharp
public sealed record CmaProcessingRequest(
    string AgentId,
    Lead Lead,
    string CorrelationId,
    CmaPipelineContext? Context = null);
```

Note: `LeadEnrichment` and `LeadScore` move INTO the context instead of being separate params.

- [ ] **Step 2: Refactor ProcessCmaAsync to use context + checkpoints**
- [ ] **Step 3: Update tests**
- [ ] **Step 4: Commit**

```
refactor: migrate CmaProcessingWorker to CmaPipelineContext
```

---

### Task 10: Migrate HomeSearchProcessingWorker to HomeSearchPipelineContext

Similar to Task 9.

```
refactor: migrate HomeSearchProcessingWorker to HomeSearchPipelineContext
```

---

## Phase 3: Fan-Out at Endpoint (Task 11)

### Task 11: Move CMA/HomeSearch dispatch from worker to endpoint

**Files:**
- Modify: `apps/api/RealEstateStar.Api/Features/Leads/Submit/SubmitLeadEndpoint.cs`
- Modify: `apps/api/RealEstateStar.Workers.Leads/LeadProcessingWorker.cs` (remove dispatch methods)
- Modify: `apps/api/RealEstateStar.Workers.Cma/CmaProcessingChannel.cs` (remove Enrichment/Score from request)
- Modify: `apps/api/RealEstateStar.Workers.Cma/CmaProcessingWorker.cs` (default score)

- [ ] **Step 1: Endpoint fans out to all 3 channels**

```csharp
// In SubmitLeadEndpoint.Handle, after save + consent:
await processingChannel.Writer.WriteAsync(new LeadProcessingRequest(agentId, lead, correlationId), ct);

if (lead.LeadType is LeadType.Seller or LeadType.Both && lead.SellerDetails is not null)
    await cmaChannel.Writer.WriteAsync(new CmaProcessingRequest(agentId, lead, correlationId), ct);

if (lead.LeadType is LeadType.Buyer or LeadType.Both && lead.BuyerDetails is not null)
    await homeSearchChannel.Writer.WriteAsync(new HomeSearchProcessingRequest(agentId, lead, correlationId), ct);
```

- [ ] **Step 2: Remove dispatch methods from LeadProcessingWorker**
- [ ] **Step 3: Simplify CmaProcessingRequest — remove Enrichment/Score params**

```csharp
public sealed record CmaProcessingRequest(
    string AgentId,
    Lead Lead,
    string CorrelationId);
```

- [ ] **Step 4: CmaProcessingWorker uses `LeadScore.Default()` for report type**
- [ ] **Step 5: Update tests + architecture diagram**
- [ ] **Step 6: Final commit**

```
refactor: fan out to all 3 pipelines from endpoint — no chaining between workers
```

---

## Summary

| Phase | Tasks | What it does |
|-------|-------|-------------|
| **Phase 1** | 1-4 | ScraperAPI observability, config, rate-limit circuit breaker |
| **Phase 2** | 5-8 | Pipeline context base class + lead worker migration |
| **Phase 3** | 9-11 | CMA + HomeSearch migration + cross-pipeline context passing |

**Key benefits:**
- Every ScraperAPI call tracked via OTel (`scraper.calls_total`, `scraper.credits_used`)
- Rate-limit circuit breaker stops burning credits when limit hit
- URLs configurable via `appsettings.json` — no code changes to swap sources
- Pipeline context carries intermediate results — no redundant API calls
- Steps check `ctx.HasCompleted()` — retries skip finished steps
- Enrichment data flows from lead → CMA/HomeSearch without re-running Claude
