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
  },
  "Pipeline": {
    "Retry": {
      "MaxRetries": 3,
      "BaseDelaySeconds": 30,
      "MaxDelaySeconds": 600,
      "BackoffMultiplier": 2.0
    }
  }
}
```

The retry config is shared across all pipeline workers via `IOptions<PipelineRetryOptions>`. Exponential backoff: attempt 1 → 30s, attempt 2 → 60s, attempt 3 → 120s, capped at 600s.
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

### Task 5: PipelineWorker base class + PipelineContext

**Files:**
- Create: `apps/api/RealEstateStar.Workers.Shared/Context/PipelineContext.cs`
- Create: `apps/api/RealEstateStar.Workers.Shared/Context/PipelineStepStatus.cs`
- Create: `apps/api/RealEstateStar.Workers.Shared/PipelineWorker.cs`

- [ ] **Step 1: Write PipelineWorker base class**

```csharp
// Workers.Shared/PipelineWorker.cs
namespace RealEstateStar.Workers.Shared;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RealEstateStar.Workers.Shared.Context;

/// <summary>
/// Configurable retry policy for pipeline workers.
/// </summary>
public class PipelineRetryOptions
{
    public int MaxRetries { get; init; } = 3;
    public int BaseDelaySeconds { get; init; } = 30;
    public int MaxDelaySeconds { get; init; } = 600; // 10 min cap
    public double BackoffMultiplier { get; init; } = 2.0;

    /// <summary>
    /// Calculate delay for a given attempt using exponential backoff.
    /// Attempt 1: 30s, Attempt 2: 60s, Attempt 3: 120s, capped at MaxDelaySeconds.
    /// </summary>
    public TimeSpan GetDelay(int attempt) =>
        TimeSpan.FromSeconds(Math.Min(
            BaseDelaySeconds * Math.Pow(BackoffMultiplier, attempt - 1),
            MaxDelaySeconds));
}

/// <summary>
/// Base class for all background pipeline workers. Enforces a consistent pattern:
/// read from channel → build context → execute steps (checkpoint/resume) →
/// exponential backoff retry → dead letter → health tracking.
/// </summary>
public abstract class PipelineWorker<TRequest, TContext>(
    ProcessingChannelBase<TRequest> channel,
    BackgroundServiceHealthTracker healthTracker,
    ILogger logger,
    IOptions<PipelineRetryOptions>? retryOptions = null) : BackgroundService
    where TContext : PipelineContext
{
    private readonly PipelineRetryOptions _retryOptions = retryOptions?.Value ?? new();

    protected abstract string WorkerName { get; }
    protected abstract TContext CreateContext(TRequest request);
    protected abstract Task ProcessAsync(TContext context, CancellationToken ct);

    /// <summary>
    /// Called when all retries exhausted. Override to write to dead letter store.
    /// </summary>
    protected virtual Task OnPermanentFailureAsync(TContext context, Exception lastException, CancellationToken ct) =>
        Task.CompletedTask;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[{Worker}-001] {Worker} started. MaxRetries: {MaxRetries}, BaseDelay: {BaseDelay}s",
            WorkerName, WorkerName, _retryOptions.MaxRetries, _retryOptions.BaseDelaySeconds);

        await foreach (var request in channel.Reader.ReadAllAsync(stoppingToken))
        {
            var ctx = CreateContext(request);
            await ExecuteWithRetryAsync(ctx, stoppingToken);
        }

        logger.LogInformation("[{Worker}-003] {Worker} stopping.", WorkerName, WorkerName);
    }

    private async Task ExecuteWithRetryAsync(TContext ctx, CancellationToken ct)
    {
        ctx.PipelineStartedAt ??= DateTime.UtcNow;

        for (var attempt = ctx.AttemptNumber; attempt <= _retryOptions.MaxRetries + 1; attempt++)
        {
            ctx.AttemptNumber = attempt;

            if (attempt > 1)
            {
                var delay = _retryOptions.GetDelay(attempt - 1);
                logger.LogInformation(
                    "[{Worker}-004] Retry {Attempt}/{Max} after {Delay}s. Failures: {Failures}. Steps: {StepSummary}. CorrelationId: {CorrelationId}",
                    WorkerName, attempt, _retryOptions.MaxRetries + 1, delay.TotalSeconds,
                    ctx.TotalFailures, FormatStepSummary(ctx), ctx.CorrelationId);
                await Task.Delay(delay, ct);
            }

            try
            {
                await ProcessAsync(ctx, ct);
                ctx.PipelineCompletedAt = DateTime.UtcNow;
                healthTracker.RecordActivity(WorkerName);

                logger.LogInformation(
                    "[{Worker}-010] Pipeline complete. Attempt: {Attempt}. Duration: {DurationMs}ms. Steps: {StepSummary}. CorrelationId: {CorrelationId}",
                    WorkerName, attempt, ctx.PipelineDurationMs, FormatStepSummary(ctx), ctx.CorrelationId);
                return; // success
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                ctx.TotalFailures++;
                ctx.LastFailedAt = DateTime.UtcNow;

                logger.LogWarning(ex,
                    "[{Worker}-005] Attempt {Attempt} failed after {DurationMs}ms. Failures: {Failures}. Steps: {StepSummary}. CorrelationId: {CorrelationId}",
                    WorkerName, attempt, (DateTime.UtcNow - ctx.PipelineStartedAt!.Value).TotalMilliseconds,
                    ctx.TotalFailures, FormatStepSummary(ctx), ctx.CorrelationId);
            }
        }

        // All retries exhausted
        ctx.PipelineCompletedAt = DateTime.UtcNow;
        logger.LogError(
            "[{Worker}-006] Permanently failed after {Attempts} attempts, {Failures} failures, {DurationMs}ms. Steps: {StepSummary}. CorrelationId: {CorrelationId}",
            WorkerName, ctx.AttemptNumber, ctx.TotalFailures, ctx.PipelineDurationMs,
            FormatStepSummary(ctx), ctx.CorrelationId);

        try
        {
            await OnPermanentFailureAsync(ctx, new InvalidOperationException($"Pipeline permanently failed after {ctx.TotalFailures} failures"), ct);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "[{Worker}-007] Dead letter write ALSO failed. CorrelationId: {CorrelationId}",
                WorkerName, ctx.CorrelationId);
        }
    }

    /// <summary>
    /// Run a named step only if it hasn't already completed (checkpoint/resume).
    /// Tracks start/end time, updates context step status.
    /// For partially completed steps, the step's action checks ctx.HasCompletedSubStep()
    /// to skip sub-work that was already done.
    /// </summary>
    protected async Task RunStepAsync(TContext ctx, string stepName, Func<Task> action, CancellationToken ct)
    {
        var step = ctx.GetOrCreateStep(stepName);

        if (step.Status == PipelineStepStatus.Completed)
        {
            logger.LogInformation("[{Worker}] Skipping '{Step}' — already completed ({DurationMs}ms). CorrelationId: {CorrelationId}",
                WorkerName, stepName, step.DurationMs, ctx.CorrelationId);
            return;
        }

        // PartiallyCompleted or Pending — run the step (action checks sub-steps internally)
        if (step.Status == PipelineStepStatus.PartiallyCompleted)
        {
            logger.LogInformation("[{Worker}] Resuming '{Step}' — partially completed ({SubSteps} sub-steps done). CorrelationId: {CorrelationId}",
                WorkerName, stepName, step.CompletedSubSteps.Count, ctx.CorrelationId);
        }

        step.StartedAt ??= DateTime.UtcNow; // preserve original start on resume
        step.Status = PipelineStepStatus.InProgress;

        try
        {
            await action();
            step.Status = PipelineStepStatus.Completed;
            step.CompletedAt = DateTime.UtcNow;

            logger.LogInformation("[{Worker}] Step '{Step}' completed in {DurationMs}ms. CorrelationId: {CorrelationId}",
                WorkerName, stepName, step.DurationMs, ctx.CorrelationId);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            step.Status = step.CompletedSubSteps.Count > 0
                ? PipelineStepStatus.PartiallyCompleted
                : PipelineStepStatus.Failed;
            step.CompletedAt = DateTime.UtcNow;
            step.Error = ex.Message;

            logger.LogError(ex, "[{Worker}] Step '{Step}' {Status} after {DurationMs}ms. SubSteps done: {SubSteps}. CorrelationId: {CorrelationId}",
                WorkerName, stepName, step.Status, step.DurationMs, string.Join(",", step.CompletedSubSteps), ctx.CorrelationId);
            throw;
        }
    }

    /// <summary>
    /// Format step summary for pipeline completion log.
    /// e.g. "enrich:Completed(23401ms), draft-email:Completed(12ms), notify:Failed(90123ms)"
    /// </summary>
    private static string FormatStepSummary(TContext ctx) =>
        string.Join(", ", ctx.Steps.Values.Select(s =>
            $"{s.Name}:{s.Status}({s.DurationMs?.ToString("F0") ?? "?"}ms)"));
}
```

The key contract:
- **`CreateContext`**: Build a typed context from the channel request
- **`ProcessAsync`**: Define pipeline steps using `RunStepAsync` for checkpoint/resume
- **`RunStepAsync`**: Skips completed steps, updates status, logs consistently
- **Base handles**: Channel read loop, health tracking, error logging, worker lifecycle

- [ ] **Step 2: Write PipelineContext (base + generic)**

```csharp
// Workers.Shared/Context/PipelineContext.cs
namespace RealEstateStar.Workers.Shared.Context;

/// <summary>
/// Non-generic base so PipelineWorker can reference without knowing TRequest.
/// Tracks pipeline-level timing, per-step timing, and retry history.
/// </summary>
public abstract class PipelineContext
{
    public required string AgentId { get; init; }
    public required string CorrelationId { get; init; }

    // Retry tracking — incremented each time this context re-enters the pipeline
    public int AttemptNumber { get; set; } = 1;
    public int TotalFailures { get; set; } = 0;
    public DateTime? LastFailedAt { get; set; }

    // Pipeline-level timing
    public DateTime? PipelineStartedAt { get; set; }
    public DateTime? PipelineCompletedAt { get; set; }
    public double? PipelineDurationMs => PipelineStartedAt.HasValue && PipelineCompletedAt.HasValue
        ? (PipelineCompletedAt.Value - PipelineStartedAt.Value).TotalMilliseconds
        : null;

    // Per-step tracking (ordered by insertion)
    public Dictionary<string, StepRecord> Steps { get; } = new();

    // Intermediate data — accumulated as steps complete
    public Dictionary<string, object> Data { get; } = new();

    // --- Data accessors ---

    public T? Get<T>(string key) where T : class =>
        Data.TryGetValue(key, out var value) ? value as T : null;

    public void Set<T>(string key, T value) where T : class =>
        Data[key] = value;

    // --- Step tracking ---

    public StepRecord GetOrCreateStep(string stepName)
    {
        if (!Steps.TryGetValue(stepName, out var record))
        {
            record = new StepRecord { Name = stepName };
            Steps[stepName] = record;
        }
        return record;
    }

    public bool HasCompleted(string stepName) =>
        Steps.TryGetValue(stepName, out var s) && s.Status == PipelineStepStatus.Completed;

    public bool HasPartiallyCompleted(string stepName) =>
        Steps.TryGetValue(stepName, out var s) && s.Status == PipelineStepStatus.PartiallyCompleted;

    /// <summary>
    /// Check if a specific sub-step within a step has been done.
    /// Used for partial completion — e.g., "enrich" step has sub-steps
    /// "scrape-google", "call-claude", "save-file". On retry, skip the
    /// sub-steps that already completed.
    /// </summary>
    public bool HasCompletedSubStep(string stepName, string subStepName) =>
        Steps.TryGetValue(stepName, out var s) && s.CompletedSubSteps.Contains(subStepName);

    public void MarkSubStepCompleted(string stepName, string subStepName)
    {
        var step = GetOrCreateStep(stepName);
        step.CompletedSubSteps.Add(subStepName);
        if (step.Status == PipelineStepStatus.Pending)
            step.Status = PipelineStepStatus.InProgress;
    }
}

/// <summary>
/// Generic context that carries the original request object.
/// </summary>
public abstract class PipelineContext<TRequest> : PipelineContext
{
    public required TRequest Request { get; init; }
}
```

```csharp
// Workers.Shared/Context/PipelineStepStatus.cs
namespace RealEstateStar.Workers.Shared.Context;

public enum PipelineStepStatus { Pending, InProgress, PartiallyCompleted, Completed, Failed, Skipped }
```

```csharp
// Workers.Shared/Context/StepRecord.cs
namespace RealEstateStar.Workers.Shared.Context;

/// <summary>
/// Tracks timing and completion state for a single pipeline step.
/// PartiallyCompleted means the step started, did some sub-work (saved in context),
/// but didn't finish. On retry, the step checks context for what was already done.
/// </summary>
public class StepRecord
{
    public required string Name { get; init; }
    public PipelineStepStatus Status { get; set; } = PipelineStepStatus.Pending;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public double? DurationMs => StartedAt.HasValue && CompletedAt.HasValue
        ? (CompletedAt.Value - StartedAt.Value).TotalMilliseconds
        : null;
    public string? Error { get; set; }
    public HashSet<string> CompletedSubSteps { get; } = [];
}
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

### Task 8: Migrate LeadProcessingWorker to inherit PipelineWorker

**Files:**
- Modify: `apps/api/RealEstateStar.Workers.Leads/LeadProcessingWorker.cs`
- Modify: `apps/api/RealEstateStar.Workers.Leads/LeadProcessingChannel.cs`
- Modify: test files

- [ ] **Step 1: Refactor LeadProcessingWorker to inherit PipelineWorker**

```csharp
public sealed class LeadProcessingWorker(
    LeadProcessingChannel channel,
    ILeadStore leadStore,
    ILeadEnricher enricher,
    ILeadNotifier notifier,
    IFailedNotificationStore failedNotificationStore,
    IFileStorageProvider storage,
    BackgroundServiceHealthTracker healthTracker,
    ILogger<LeadProcessingWorker> logger)
    : PipelineWorker<LeadProcessingRequest, LeadPipelineContext>(channel, healthTracker, logger)
{
    protected override string WorkerName => "LeadWorker";

    protected override LeadPipelineContext CreateContext(LeadProcessingRequest request) => new()
    {
        Request = request.Lead,
        AgentId = request.AgentId,
        CorrelationId = request.CorrelationId,
    };

    protected override async Task ProcessAsync(LeadPipelineContext ctx, CancellationToken ct)
    {
        await RunStepAsync(ctx, "enrich", () => EnrichAsync(ctx, ct), ct);
        await RunStepAsync(ctx, "draft-email", () => DraftEmailAsync(ctx, ct), ct);
        await RunStepAsync(ctx, "notify", () => NotifyAsync(ctx, ct), ct);
    }

    private async Task EnrichAsync(LeadPipelineContext ctx, CancellationToken ct)
    {
        // Sub-step: scrape (expensive — check if already done)
        if (!ctx.HasCompletedSubStep("enrich", "scrape"))
        {
            var (enrichment, score) = await enricher.EnrichAsync(ctx.Request, ct);
            ctx.Enrichment = enrichment;
            ctx.Score = score;
            ctx.MarkSubStepCompleted("enrich", "scrape");
        }

        // Sub-step: save to disk (idempotent but tracked)
        if (!ctx.HasCompletedSubStep("enrich", "save"))
        {
            await leadStore.UpdateEnrichmentAsync(ctx.Request, ctx.Enrichment!, ctx.Score!, ct);
            ctx.MarkSubStepCompleted("enrich", "save");
        }
    }

    private async Task DraftEmailAsync(LeadPipelineContext ctx, CancellationToken ct)
    {
        var subject = notifier.BuildSubject(ctx.Request, ctx.Enrichment!, ctx.Score!);
        var body = notifier.BuildBody(ctx.Request, ctx.Enrichment!, ctx.Score!);
        ctx.EmailDraftSubject = subject;
        ctx.EmailDraftBody = body;
        // Save draft to disk...
    }

    private async Task NotifyAsync(LeadPipelineContext ctx, CancellationToken ct)
    {
        // Uses ctx.EmailDraftSubject/Body — already built, no regeneration
        await notifier.NotifyAgentAsync(ctx.AgentId, ctx.Request, ctx.Enrichment!, ctx.Score!, ct);
    }
}
```

Every worker follows the same pattern: inherit `PipelineWorker`, define steps via `RunStepAsync`, store results in context.

- [ ] **Step 2: Update tests**
- [ ] **Step 3: Run all tests, verify pass**
- [ ] **Step 4: Commit**

```
refactor: migrate LeadProcessingWorker to PipelineWorker base class
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

- [ ] **Step 4: Simplify DetermineReportType — remove LeadScore entirely**

```csharp
internal static ReportType DetermineReportType(int compCount) =>
    compCount switch
    {
        >= 6 => ReportType.Comprehensive,
        >= 3 => ReportType.Standard,
        _ => ReportType.Lean
    };
```

Report type is driven by comp data quality, not lead motivation. A seller with 8 comps always gets Comprehensive regardless of score.
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
