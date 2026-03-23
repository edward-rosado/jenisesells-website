# API Observability Dashboard + Scraper Improvements

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Full-system Grafana dashboard covering 60+ OTel metrics, centralized Claude token tracking across all 6 callers, circuit breaker auto-reset, and configurable source URLs.

**Architecture:** New `ClaudeDiagnostics` in Domain provides centralized token/cost counters tagged by pipeline and model. Each of the 6 Claude callers emits to both centralized and per-pipeline counters. ScraperClient gets a timed circuit breaker reset. Source URLs read from `appsettings.json` instead of hardcoded. A Grafana dashboard JSON covers all subsystems.

**Tech Stack:** .NET 10, OpenTelemetry Metrics, Grafana (Mimir/OTLP), System.Threading.Interlocked

**Design Spec:** `docs/superpowers/specs/2026-03-23-api-observability-dashboard-design.md`

---

## File Map

### New Files

| File | Responsibility |
|------|---------------|
| `apps/api/RealEstateStar.Domain/Shared/ClaudeDiagnostics.cs` | Centralized Claude token/cost/call counters with `RecordUsage` helper |
| `apps/api/tests/RealEstateStar.Domain.Tests/Shared/ClaudeDiagnosticsTests.cs` | Tests for RecordUsage, cost calculation, tagging |
| `infra/grafana/real-estate-star-api-dashboard.json` | Full-system Grafana dashboard (importable JSON) |

### Modified Files

| File | Change |
|------|--------|
| `apps/api/RealEstateStar.Clients.Scraper/ScraperClient.cs` | Replace `_available` bool with `_rateLimitedAtTicks` long, add timed reset |
| `apps/api/RealEstateStar.Clients.Scraper/ScraperOptions.cs` | Add `CircuitBreakerResetSeconds` property |
| `apps/api/RealEstateStar.Domain/Cma/CmaDiagnostics.cs` | Add `cma.llm_tokens.input`, `cma.llm_tokens.output`, `cma.llm_cost_usd` |
| `apps/api/RealEstateStar.Domain/HomeSearch/HomeSearchDiagnostics.cs` | Add `home_search.llm_tokens.input`, `home_search.llm_tokens.output` |
| `apps/api/RealEstateStar.Domain/Onboarding/OnboardingDiagnostics.cs` | Add `onboarding.llm_tokens.input`, `onboarding.llm_tokens.output` |
| `apps/api/RealEstateStar.Workers.Leads/ScraperLeadEnricher.cs` | Add `ClaudeDiagnostics.RecordUsage` call, accept `Dictionary<string, string> sourceUrls` |
| `apps/api/RealEstateStar.Workers.Cma/ClaudeCmaAnalyzer.cs` | Add token parsing + `ClaudeDiagnostics.RecordUsage` + `CmaDiagnostics` LLM counters |
| `apps/api/RealEstateStar.Workers.Cma/ScraperCompSource.cs` | Add token parsing + `ClaudeDiagnostics.RecordUsage` + `CmaDiagnostics` LLM counters |
| `apps/api/RealEstateStar.Workers.HomeSearch/ScraperHomeSearchProvider.cs` | Accept `Dictionary<string, string> sourceUrls`, add token parsing + counters, generic `BuildSearchUrl` |
| `apps/api/RealEstateStar.Api/Features/Onboarding/Services/OnboardingChatService.cs` | Add streaming token parsing + `ClaudeDiagnostics.RecordUsage` |
| `apps/api/RealEstateStar.DataServices/Onboarding/ProfileScraperService.cs` | Add token parsing + `ClaudeDiagnostics.RecordUsage` |
| `apps/api/RealEstateStar.Api/Program.cs` | Read source URLs from config, dynamic CompSource registration, register ClaudeDiagnostics |
| `apps/api/RealEstateStar.Api/appsettings.json` | Add `CircuitBreakerResetSeconds`, update HomeSearch source URLs with filter placeholders |
| `apps/api/RealEstateStar.Api/Diagnostics/OpenTelemetryExtensions.cs` | Register WhatsAppDiagnostics + ClaudeDiagnostics source + meter |

---

## Phase 1: Foundation (Tasks 1-3) — Independent, can run in parallel

### Task 1: ClaudeDiagnostics centralized counters

**Files:**
- Create: `apps/api/RealEstateStar.Domain/Shared/ClaudeDiagnostics.cs`
- Create: `apps/api/tests/RealEstateStar.Domain.Tests/Shared/ClaudeDiagnosticsTests.cs`

- [ ] **Step 1: Write ClaudeDiagnostics**

```csharp
// Domain/Shared/ClaudeDiagnostics.cs
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RealEstateStar.Domain.Shared;

public static class ClaudeDiagnostics
{
    public const string ServiceName = "RealEstateStar.Claude";
    public static readonly ActivitySource ActivitySource = new(ServiceName, "1.0.0");
    private static readonly Meter Meter = new(ServiceName, "1.0.0");

    public static readonly Counter<long> TokensInput = Meter.CreateCounter<long>(
        "claude.tokens.input", description: "Claude API input tokens consumed");
    public static readonly Counter<long> TokensOutput = Meter.CreateCounter<long>(
        "claude.tokens.output", description: "Claude API output tokens consumed");
    public static readonly Counter<double> CostUsd = Meter.CreateCounter<double>(
        "claude.cost_usd", unit: "USD", description: "Estimated Claude API cost");
    public static readonly Counter<long> CallsTotal = Meter.CreateCounter<long>(
        "claude.calls_total", description: "Total Claude API calls");
    public static readonly Counter<long> CallsFailed = Meter.CreateCounter<long>(
        "claude.calls_failed", description: "Failed Claude API calls");
    public static readonly Histogram<double> CallDuration = Meter.CreateHistogram<double>(
        "claude.call_duration_ms", description: "Claude API call duration in milliseconds");

    /// <summary>
    /// Record token usage to centralized counters. Tagged by pipeline and model.
    /// Cost: sonnet = $3/MTok in + $15/MTok out, haiku = $0.80/MTok in + $4/MTok out.
    /// </summary>
    public static void RecordUsage(string pipeline, string model, int inputTokens, int outputTokens, double durationMs)
    {
        var tags = new TagList { { "pipeline", pipeline }, { "model", model } };

        TokensInput.Add(inputTokens, tags);
        TokensOutput.Add(outputTokens, tags);
        CallsTotal.Add(1, tags);
        CallDuration.Record(durationMs, tags);

        var (inputRate, outputRate) = model switch
        {
            var m when m.Contains("haiku") => (0.80 / 1_000_000, 4.0 / 1_000_000),
            _ => (3.0 / 1_000_000, 15.0 / 1_000_000) // sonnet default
        };
        CostUsd.Add(inputTokens * inputRate + outputTokens * outputRate, tags);
    }

    public static void RecordFailure(string pipeline, string model)
    {
        CallsFailed.Add(1, new TagList { { "pipeline", pipeline }, { "model", model } });
    }
}
```

- [ ] **Step 2: Write tests**

```csharp
// tests/RealEstateStar.Domain.Tests/Shared/ClaudeDiagnosticsTests.cs
// Tests:
// - RecordUsage_IncrementsTokenCounters
// - RecordUsage_CalculatesSonnetCostCorrectly (input=1000, output=500 → $3/M * 1000 + $15/M * 500)
// - RecordUsage_CalculatesHaikuCostCorrectly (model contains "haiku" → different rates)
// - RecordFailure_IncrementsFailedCounter
// - CounterNames_MatchExpectedOTelNames (verify "claude.tokens.input" etc.)
```

- [ ] **Step 3: Build and run tests**
- [ ] **Step 4: Commit**

```
feat: add ClaudeDiagnostics with centralized token/cost counters
```

---

### Task 2: Circuit breaker timed reset

**Files:**
- Modify: `apps/api/RealEstateStar.Clients.Scraper/ScraperClient.cs`
- Modify: `apps/api/RealEstateStar.Clients.Scraper/ScraperOptions.cs`
- Modify: `apps/api/RealEstateStar.Api/appsettings.json`
- Modify: `apps/api/tests/RealEstateStar.Clients.Scraper.Tests/ScraperClientTests.cs`

- [ ] **Step 1: Add `CircuitBreakerResetSeconds` to ScraperOptions**

```csharp
// Add to ScraperOptions.cs:
public int CircuitBreakerResetSeconds { get; init; } = 600;
```

- [ ] **Step 2: Refactor ScraperClient to use `_rateLimitedAtTicks`**

Replace:
```csharp
private volatile bool _available = true;
public bool IsAvailable => _available;
```

With:
```csharp
private long _rateLimitedAtTicks; // 0 = available, >0 = rate-limited at UTC ticks

public bool IsAvailable
{
    get
    {
        var ticks = Interlocked.Read(ref _rateLimitedAtTicks);
        if (ticks == 0) return true;
        var resetTicks = TimeSpan.FromSeconds(options.Value.CircuitBreakerResetSeconds).Ticks;
        return (DateTime.UtcNow.Ticks - ticks) > resetTicks;
    }
}
```

Update `FetchAsync` entry:
```csharp
var rateLimitedAt = Interlocked.Read(ref _rateLimitedAtTicks);
if (rateLimitedAt != 0)
{
    var resetTicks = TimeSpan.FromSeconds(options.Value.CircuitBreakerResetSeconds).Ticks;
    if ((DateTime.UtcNow.Ticks - rateLimitedAt) > resetTicks)
    {
        Interlocked.Exchange(ref _rateLimitedAtTicks, 0);
        logger.LogInformation("[SCRAPER-050] Circuit breaker reset after {Seconds}s. Scraper re-enabled.",
            options.Value.CircuitBreakerResetSeconds);
    }
    else
    {
        logger.LogWarning("[SCRAPER-010] Scraper unavailable (rate limited). Skipping {Source} for agent {AgentId}",
            source, agentId);
        return null;
    }
}
```

On 429, replace `_available = false` with:
```csharp
Interlocked.Exchange(ref _rateLimitedAtTicks, DateTime.UtcNow.Ticks);
```

- [ ] **Step 3: Add `CircuitBreakerResetSeconds` to appsettings.json Scraper section**

- [ ] **Step 4: Write/update tests**

New tests:
- `FetchAsync_ResetsAfterCooldown` — set short cooldown (1s), trigger 429, wait, verify next call makes HTTP request
- `FetchAsync_StaysUnavailableDuringCooldown` — trigger 429, immediately call again, verify null returned
- `IsAvailable_ReturnsTrueAfterReset` — trigger 429, advance time past reset, check property
- `FetchAsync_ConcurrentRateLimit_SingleReset` — two threads trigger 429, verify stable state

Update existing tests that check `_available` to work with new mechanism.

- [ ] **Step 5: Run tests, verify pass**
- [ ] **Step 6: Commit**

```
feat: add timed circuit breaker reset to ScraperClient (default 10 min)
```

---

### Task 3: Per-pipeline LLM counters

**Files:**
- Modify: `apps/api/RealEstateStar.Domain/Cma/CmaDiagnostics.cs`
- Modify: `apps/api/RealEstateStar.Domain/HomeSearch/HomeSearchDiagnostics.cs`
- Modify: `apps/api/RealEstateStar.Domain/Onboarding/OnboardingDiagnostics.cs`

- [ ] **Step 1: Add LLM counters to CmaDiagnostics**

```csharp
// Add to CmaDiagnostics.cs:
public static readonly Counter<long> LlmTokensInput = Meter.CreateCounter<long>(
    "cma.llm_tokens.input", description: "LLM input tokens consumed for CMA processing");
public static readonly Counter<long> LlmTokensOutput = Meter.CreateCounter<long>(
    "cma.llm_tokens.output", description: "LLM output tokens consumed for CMA processing");
public static readonly Counter<double> LlmCostUsd = Meter.CreateCounter<double>(
    "cma.llm_cost_usd", unit: "USD", description: "Estimated LLM cost for CMA processing");
```

- [ ] **Step 2: Add LLM counters to HomeSearchDiagnostics**

```csharp
// Add to HomeSearchDiagnostics.cs:
public static readonly Counter<long> LlmTokensInput = Meter.CreateCounter<long>(
    "home_search.llm_tokens.input", description: "LLM input tokens consumed for home search");
public static readonly Counter<long> LlmTokensOutput = Meter.CreateCounter<long>(
    "home_search.llm_tokens.output", description: "LLM output tokens consumed for home search");
```

- [ ] **Step 3: Add LLM counters to OnboardingDiagnostics**

```csharp
// Add to OnboardingDiagnostics.cs:
public static readonly Counter<long> LlmTokensInput = Meter.CreateCounter<long>(
    "onboarding.llm_tokens.input", description: "LLM input tokens consumed for onboarding");
public static readonly Counter<long> LlmTokensOutput = Meter.CreateCounter<long>(
    "onboarding.llm_tokens.output", description: "LLM output tokens consumed for onboarding");
```

- [ ] **Step 4: Build, verify**
- [ ] **Step 5: Commit**

```
feat: add per-pipeline LLM token counters to CMA, HomeSearch, Onboarding diagnostics
```

---

## Phase 2: OTel Registration + Instrumentation (Tasks 4-6)

### Task 4: Register WhatsApp + Claude diagnostics in OTel

**Files:**
- Modify: `apps/api/RealEstateStar.Api/Diagnostics/OpenTelemetryExtensions.cs`

- [ ] **Step 1: Add usings and registrations**

Add to the top:
```csharp
using RealEstateStar.Domain.WhatsApp;
using RealEstateStar.Domain.Shared;
```

In `.WithTracing(...)`, add after `HomeSearchDiagnostics` line:
```csharp
.AddSource(WhatsAppDiagnostics.ServiceName)
.AddSource(ClaudeDiagnostics.ServiceName)
```

In `.WithMetrics(...)`, add after `HomeSearchDiagnostics` line:
```csharp
.AddMeter(WhatsAppDiagnostics.ServiceName)
.AddMeter(ClaudeDiagnostics.ServiceName)
```

- [ ] **Step 2: Build, verify**
- [ ] **Step 3: Commit**

```
feat: register WhatsApp + Claude diagnostics in OpenTelemetry pipeline
```

---

### Task 5: Instrument all 6 Claude callers

**Files:**
- Modify: `apps/api/RealEstateStar.Workers.Leads/ScraperLeadEnricher.cs`
- Modify: `apps/api/RealEstateStar.Workers.Cma/ClaudeCmaAnalyzer.cs`
- Modify: `apps/api/RealEstateStar.Workers.Cma/ScraperCompSource.cs`
- Modify: `apps/api/RealEstateStar.Workers.HomeSearch/ScraperHomeSearchProvider.cs`
- Modify: `apps/api/RealEstateStar.Api/Features/Onboarding/Services/OnboardingChatService.cs`
- Modify: `apps/api/RealEstateStar.DataServices/Onboarding/ProfileScraperService.cs`

Each caller gets the same pattern — parse `usage` from Claude response JSON and emit to both centralized + per-pipeline counters.

- [ ] **Step 1: Instrument ScraperLeadEnricher**

Already parses tokens at line ~205. Add after the existing token logging:
```csharp
ClaudeDiagnostics.RecordUsage("lead", ClaudeModel, inputTokens, outputTokens, elapsedMs);
// LeadDiagnostics already records per-pipeline — no change needed
```

Add `using RealEstateStar.Domain.Shared;` and track elapsed time with `Stopwatch.GetTimestamp()` around the Claude call.

- [ ] **Step 2: Instrument ClaudeCmaAnalyzer**

After `response.Content.ReadAsStringAsync()`, parse tokens:
```csharp
if (doc.RootElement.TryGetProperty("usage", out var usage))
{
    var inputTokens = usage.TryGetProperty("input_tokens", out var it) ? it.GetInt32() : 0;
    var outputTokens = usage.TryGetProperty("output_tokens", out var ot) ? ot.GetInt32() : 0;
    ClaudeDiagnostics.RecordUsage("cma-analysis", Model, inputTokens, outputTokens, elapsedMs);
    CmaDiagnostics.LlmTokensInput.Add(inputTokens);
    CmaDiagnostics.LlmTokensOutput.Add(outputTokens);
}
```

Add `using RealEstateStar.Domain.Shared;` and `using RealEstateStar.Domain.Cma;`.
Wrap the Claude HTTP call with `Stopwatch.GetTimestamp()` for `elapsedMs`.

- [ ] **Step 3: Instrument ScraperCompSource**

Same pattern as ClaudeCmaAnalyzer, after the Claude extraction call. Use pipeline tag `"cma-comps"`:
```csharp
ClaudeDiagnostics.RecordUsage("cma-comps", ClaudeModel, inputTokens, outputTokens, elapsedMs);
CmaDiagnostics.LlmTokensInput.Add(inputTokens);
CmaDiagnostics.LlmTokensOutput.Add(outputTokens);
```

- [ ] **Step 4: Instrument ScraperHomeSearchProvider**

After `CurateWithClaudeAsync`, parse tokens. Use pipeline tag `"home-search"`:
```csharp
ClaudeDiagnostics.RecordUsage("home-search", ClaudeModel, inputTokens, outputTokens, elapsedMs);
HomeSearchDiagnostics.LlmTokensInput.Add(inputTokens);
HomeSearchDiagnostics.LlmTokensOutput.Add(outputTokens);
```

- [ ] **Step 5: Instrument OnboardingChatService**

This uses **streaming** — tokens come from the final `message_delta` SSE event. After the stream completes, the service has the full response. Look for where `usage` data arrives in the stream and emit:
```csharp
ClaudeDiagnostics.RecordUsage("onboarding", Model, inputTokens, outputTokens, elapsedMs);
OnboardingDiagnostics.LlmTokensInput.Add(inputTokens);
OnboardingDiagnostics.LlmTokensOutput.Add(outputTokens);
```

Read the streaming implementation to find where the final usage event is available. If the current streaming code doesn't capture usage, add parsing of the `message_delta` event's `usage` field.

- [ ] **Step 6: Instrument ProfileScraperService**

Same pattern. Uses `claude-haiku-4-5-20251001` model. Pipeline tag `"profile-scraper"`:
```csharp
ClaudeDiagnostics.RecordUsage("profile-scraper", Model, inputTokens, outputTokens, elapsedMs);
OnboardingDiagnostics.LlmTokensInput.Add(inputTokens);
OnboardingDiagnostics.LlmTokensOutput.Add(outputTokens);
```

- [ ] **Step 7: Build entire solution**

```bash
dotnet build apps/api/RealEstateStar.Api/RealEstateStar.Api.csproj
```

- [ ] **Step 8: Commit**

```
feat: instrument all 6 Claude callers with centralized + per-pipeline token tracking
```

---

### Task 6: Configurable source URLs

**Files:**
- Modify: `apps/api/RealEstateStar.Workers.Leads/ScraperLeadEnricher.cs`
- Modify: `apps/api/RealEstateStar.Workers.HomeSearch/ScraperHomeSearchProvider.cs`
- Modify: `apps/api/RealEstateStar.Api/Program.cs`
- Modify: `apps/api/RealEstateStar.Api/appsettings.json`

- [ ] **Step 1: Update ScraperLeadEnricher to accept source URLs**

Add `Dictionary<string, string> sourceUrls` to constructor (after `IScraperClient`). Replace the hardcoded Google URL in `ScrapeAllSourcesAsync`:

```csharp
// Before:
var googleUrl = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}";

// After — iterate over configured search engines:
foreach (var (engineName, urlTemplate) in sourceUrls)
{
    var url = urlTemplate.Replace("{query}", Uri.EscapeDataString(query));
    // FetchAsync(url, $"{engineName}-{queryName}", "enrichment", cts.Token)
}
```

Each engine runs all 8 query variants. Currently only `google` is configured.

- [ ] **Step 2: Update ScraperHomeSearchProvider to accept source URLs**

Add `Dictionary<string, string> sourceUrls` to constructor. Replace `BuildZillowUrl()`, `BuildRedfinUrl()`, `BuildMlsUrl()` with generic `BuildSearchUrl`:

```csharp
internal static string BuildSearchUrl(string template, HomeSearchCriteria criteria)
{
    var url = template
        .Replace("{area}", Uri.EscapeDataString(criteria.Area))
        .Replace("{minPrice}", criteria.MinPrice?.ToString() ?? "")
        .Replace("{maxPrice}", criteria.MaxPrice?.ToString() ?? "")
        .Replace("{minBeds}", criteria.MinBeds?.ToString() ?? "")
        .Replace("{minBaths}", criteria.MinBaths?.ToString() ?? "");

    // Remove query params with empty values
    var uri = new UriBuilder(url);
    var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
    var cleanParams = queryParams.AllKeys
        .Where(k => !string.IsNullOrEmpty(queryParams[k]))
        .Select(k => $"{k}={queryParams[k]}");
    uri.Query = string.Join("&", cleanParams);
    return uri.ToString();
}
```

Replace search task creation:
```csharp
var searchTasks = sourceUrls.Select(kvp =>
    FetchFromSourceAsync(kvp.Key, BuildSearchUrl(kvp.Value, criteria), criteria, ct));
```

Remove `BuildZillowUrl`, `BuildRedfinUrl`, `BuildMlsUrl` methods.

- [ ] **Step 3: Update Program.cs DI registration**

Read source URLs from config and pass to constructors:

```csharp
// Lead sources
var leadSources = builder.Configuration.GetSection("Pipeline:Lead:Sources")
    .Get<Dictionary<string, string>>() ?? new();

// Home search sources
var homeSearchSources = builder.Configuration.GetSection("Pipeline:HomeSearch:Sources")
    .Get<Dictionary<string, string>>() ?? new();

// CMA sources — dynamic CompSource registration
var cmaSources = builder.Configuration.GetSection("Pipeline:Cma:Sources")
    .Get<Dictionary<string, string>>() ?? new();

foreach (var (sourceName, urlPattern) in cmaSources)
{
    if (!Enum.TryParse<CompSource>(sourceName, ignoreCase: true, out var source))
    {
        logger.LogWarning("[STARTUP-060] Unknown comp source '{SourceName}' in config, skipping", sourceName);
        continue;
    }
    builder.Services.AddSingleton<ICompSource>(sp =>
        new ScraperCompSource(..., source, urlPattern, ...));
}
```

Remove the 3 hardcoded `ScraperCompSource` registrations. Update `ScraperLeadEnricher` and `ScraperHomeSearchProvider` factory registrations to pass source URL dictionaries.

- [ ] **Step 4: Update appsettings.json**

Update `Pipeline:HomeSearch:Sources` to include full URL templates with per-source filter params:
```json
"HomeSearch": {
    "Sources": {
        "zillow": "https://www.zillow.com/homes/{area}_rb/?price-min={minPrice}&price-max={maxPrice}&beds-min={minBeds}&baths-min={minBaths}",
        "redfin": "https://www.redfin.com/city/{area}?min_price={minPrice}&max_price={maxPrice}&num_beds={minBeds}&num_baths={minBaths}",
        "realtor": "https://www.realtor.com/realestateandhomes-search/{area}?price-min={minPrice}&price-max={maxPrice}&beds-min={minBeds}&baths-min={minBaths}"
    }
}
```

Change `Pipeline:Cma:Sources` key `"realtor"` to `"realtorcom"` to match the `CompSource` enum.

- [ ] **Step 5: Build and run tests**
- [ ] **Step 6: Update tests for new constructors** (ScraperLeadEnricherTests, ScraperHomeSearchProviderTests)
- [ ] **Step 7: Commit**

```
refactor: read source URLs from config — no more hardcoded Zillow/Redfin/Realtor URLs
```

---

## Phase 3: Dashboard + Tests (Tasks 7-8)

### Task 7: Grafana dashboard JSON

**Files:**
- Create: `infra/grafana/real-estate-star-api-dashboard.json`

- [ ] **Step 1: Create the Grafana dashboard JSON**

Generate a valid Grafana dashboard JSON with 7 rows, 35+ panels. Use `${DS_METRICS}` as the datasource variable (Mimir-compatible).

**Row layout** (from design spec):

1. **System Health** (collapsed): Onboarding sessions, state transitions, consent recorded, audit failures
2. **Lead Pipeline**: Leads received, enrichment success/fail, notifications, step durations, pipeline duration, form funnel
3. **CMA Pipeline**: Generated/failed, comps found, step durations (including drive), total duration
4. **Home Search**: Completed/failed, listings found, step durations (including drive), total duration
5. **Claude / LLM**: Tokens by pipeline (stacked), cost by pipeline (stacked), total cost gauge, calls by model, failure rate, call duration
6. **ScraperAPI**: Calls by source, success/fail, rate limited, credits used, duration heatmap
7. **WhatsApp**: Messages in/out, duplicates, queue health, webhook fails, processing latency, intent/audit

Dashboard settings: auto-refresh 30s, default range 6h, tags: `real-estate-star`, `api`, `observability`.

All metric names use underscore format (OTel dot → Mimir underscore conversion).

- [ ] **Step 2: Commit**

```
feat: add full-system Grafana dashboard with 7 rows and 35+ panels
```

---

### Task 8: Update existing tests + full test run

**Files:**
- Modify: Various test files affected by constructor changes

- [ ] **Step 1: Update ScraperLeadEnricherTests for new `sourceUrls` parameter**

Add `new Dictionary<string, string> { ["google"] = "https://www.google.com/search?q={query}" }` to test constructor calls.

- [ ] **Step 2: Update ScraperHomeSearchProviderTests**

- Replace `BuildZillowUrl`, `BuildRedfinUrl`, `BuildMlsUrl` tests with `BuildSearchUrl` tests
- Add test constructor calls with source URLs dictionary
- Test: `BuildSearchUrl_SubstitutesArea`
- Test: `BuildSearchUrl_RemovesEmptyFilterParams`
- Test: `BuildSearchUrl_PreservesPerSourceParamNames`

- [ ] **Step 3: Update Program.cs-dependent tests if any**

Check if `SubmitLeadEndpointTests` or `BackgroundServiceHealthCheckTests` need updates for the dynamic CompSource registration.

- [ ] **Step 4: Run full test suite**

```bash
dotnet test apps/api/RealEstateStar.Api.sln --verbosity minimal
```

All tests must pass, 0 failures.

- [ ] **Step 5: Commit**

```
fix: update tests for configurable source URLs and ClaudeDiagnostics instrumentation
```

---

## Summary

| Phase | Tasks | What it does | Parallel? |
|-------|-------|-------------|-----------|
| **Phase 1** | 1, 2, 3 | Foundation: ClaudeDiagnostics, circuit breaker reset, per-pipeline LLM counters | All 3 independent |
| **Phase 2** | 4, 5, 6 | OTel registration, instrument 6 Claude callers, configurable URLs | 4 depends on 1; 5 depends on 1+3; 6 independent |
| **Phase 3** | 7, 8 | Grafana dashboard JSON, test updates | 7 independent; 8 after all |

**Parallelization opportunities:**
- Wave 1: Tasks 1 + 2 + 3 (zero overlap)
- Wave 2: Tasks 4 + 6 (4 depends on 1, 6 is independent; different files)
- Wave 3: Task 5 (depends on 1 + 3, touches 6 files)
- Wave 4: Tasks 7 + 8 in parallel (dashboard JSON is independent of test updates)
