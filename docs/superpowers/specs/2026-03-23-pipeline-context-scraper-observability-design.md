# Pipeline Context + ScraperAPI Observability — Design Spec

**Author:** Eddie Rosado
**Date:** 2026-03-23
**Status:** Draft
**Plan:** `docs/superpowers/plans/2026-03-23-pipeline-context-scraper-observability.md`

---

## Problem Statement

The current pipeline architecture has three critical gaps:

1. **No ScraperAPI observability** — We hit 70% usage on a single test submission with zero visibility. No counters, no rate-limit handling, no configurable URLs. When limits are reached, the system keeps calling and failing silently.

2. **No shared contract for workers** — Each background service (`LeadProcessingWorker`, `CmaProcessingWorker`, `HomeSearchProcessingWorker`) implements its own channel-read loop, error handling, retry logic, and health tracking. Bugs fixed in one worker aren't automatically fixed in others.

3. **No pipeline context** — Each step runs independently, passing raw objects between methods. Intermediate results (scraped HTML, Claude responses, email drafts) are computed and discarded. On retry, everything re-runs from scratch — wasting Claude tokens and ScraperAPI credits. There's no record of what completed, what failed, or how far we got.

---

## Goals

- **Track every ScraperAPI call** with OTel counters visible in Grafana
- **Stop burning credits** when rate limits are hit (circuit breaker)
- **Make source URLs configurable** via appsettings (swap Zillow/Redfin/Realtor without code changes)
- **Single base class** for all pipeline workers with consistent logging, retry, health tracking
- **Pipeline context** that accumulates data across steps — retries resume, not restart
- **Step-level and sub-step-level tracking** — know exactly what completed, what failed, and how long each part took
- **Configurable exponential backoff** with dead letter on permanent failure

---

## Architecture Overview

```mermaid
flowchart TD
    Endpoint["API Endpoint<br/>validate → save → consent"]

    LC["LeadChannel"]
    CC["CmaChannel"]
    HC["HomeSearchChannel"]

    LW["LeadWorker<br/><i>extends PipelineWorker</i>"]
    CW["CmaWorker<br/><i>extends PipelineWorker</i>"]
    HW["HomeSearchWorker<br/><i>extends PipelineWorker</i>"]

    SC["IScraperClient<br/>OTel counters · circuit breaker · configurable URLs"]

    Endpoint -->|"fan-out"| LC
    Endpoint -->|"fan-out"| CC
    Endpoint -->|"fan-out"| HC

    LC --> LW
    CC --> CW
    HC --> HW

    LW --> SC
    CW --> SC
    HW --> SC

    style Endpoint fill:#7B68EE,color:#fff
    style LC fill:#7B68EE,color:#fff
    style CC fill:#7B68EE,color:#fff
    style HC fill:#7B68EE,color:#fff
    style LW fill:#4A90D9,color:#fff
    style CW fill:#4A90D9,color:#fff
    style HW fill:#4A90D9,color:#fff
    style SC fill:#2E7D32,color:#fff
```

All three pipelines are **independent** — no data flows between them. The API endpoint fans out to all three channels simultaneously. Each worker runs its own pipeline with its own context.

---

## Component Design

### 1. PipelineContext — The State Carrier

Every pipeline gets a context object that carries:
- The original request
- All intermediate results (enrichment, comps, drafts, PDFs)
- Step completion status with timing
- Sub-step tracking for partial completion
- Retry history with error details

```mermaid
classDiagram
    class PipelineContext {
        <<abstract>>
        +string AgentId
        +string CorrelationId
        +DateTime? PipelineStartedAt
        +DateTime? PipelineCompletedAt
        +double? PipelineDurationMs
        +int AttemptNumber
        +int TotalFailures
        +DateTime? LastFailedAt
        +Dictionary~string, object~ Data
        +Dictionary~string, StepRecord~ Steps
        +Get~T~(key) T?
        +Set~T~(key, value)
        +HasCompleted(step) bool
        +HasCompletedSubStep(step, subStep) bool
        +MarkCompleted(step)
        +MarkFailed(step)
        +MarkSubStepCompleted(step, subStep)
    }

    class PipelineContext_T["PipelineContext&lt;TRequest&gt;"] {
        <<abstract>>
        +TRequest Request
    }

    class StepRecord {
        +string Name
        +PipelineStepStatus Status
        +DateTime? StartedAt
        +DateTime? CompletedAt
        +double? DurationMs
        +string? Error
        +HashSet~string~ CompletedSubSteps
        +List~ErrorEntry~ ErrorHistory
    }

    class ErrorEntry {
        +int Attempt
        +DateTime Timestamp
        +string StepName
        +string Message
        +string? StackTrace
    }

    class LeadPipelineContext {
        +LeadEnrichment? Enrichment
        +LeadScore? Score
        +string? EmailDraftSubject
        +string? EmailDraftBody
    }

    class CmaPipelineContext {
        +List~Comp~? Comps
        +CmaAnalysis? Analysis
        +byte[]? PdfBytes
    }

    class HomeSearchPipelineContext {
        +List~Listing~? Listings
    }

    PipelineContext <|-- PipelineContext_T
    PipelineContext_T <|-- LeadPipelineContext
    PipelineContext_T <|-- CmaPipelineContext
    PipelineContext_T <|-- HomeSearchPipelineContext
    PipelineContext "1" *-- "*" StepRecord
    StepRecord "1" *-- "*" ErrorEntry
```

### 2. PipelineWorker — The Shared Contract

Every background service inherits from `PipelineWorker<TRequest, TContext>` which enforces:

```mermaid
classDiagram
    class BackgroundService {
        <<abstract>>
        #ExecuteAsync(ct) Task
    }

    class PipelineWorker_T["PipelineWorker&lt;TRequest, TContext&gt;"] {
        <<abstract>>
        #string WorkerName*
        #CreateContext(request) TContext*
        #ProcessAsync(context, ct) Task*
        #OnPermanentFailureAsync(context, ex, ct) Task
        #RunStepAsync(ctx, stepName, action, ct) Task
        #ExecuteAsync(ct) Task
        -ExecuteWithRetryAsync(ctx, ct) Task
        -FormatStepSummary(ctx) string
    }

    class LeadProcessingWorker {
        #WorkerName = "LeadWorker"
        #CreateContext(request) LeadPipelineContext
        #ProcessAsync(ctx, ct) Task
        -EnrichAsync(ctx, ct) Task
        -DraftEmailAsync(ctx, ct) Task
        -NotifyAsync(ctx, ct) Task
    }

    class CmaProcessingWorker {
        #WorkerName = "CmaWorker"
        #CreateContext(request) CmaPipelineContext
        #ProcessAsync(ctx, ct) Task
        -FetchCompsAsync(ctx, ct) Task
        -AnalyzeAsync(ctx, ct) Task
        -GeneratePdfAsync(ctx, ct) Task
        -NotifySellerAsync(ctx, ct) Task
    }

    class HomeSearchProcessingWorker {
        #WorkerName = "HomeSearchWorker"
        #CreateContext(request) HomeSearchPipelineContext
        #ProcessAsync(ctx, ct) Task
        -FetchListingsAsync(ctx, ct) Task
        -NotifyBuyerAsync(ctx, ct) Task
    }

    class PipelineRetryOptions {
        +int MaxRetries = 3
        +int BaseDelaySeconds = 30
        +int MaxDelaySeconds = 600
        +double BackoffMultiplier = 2.0
        +GetDelay(attempt) TimeSpan
    }

    BackgroundService <|-- PipelineWorker_T
    PipelineWorker_T <|-- LeadProcessingWorker
    PipelineWorker_T <|-- CmaProcessingWorker
    PipelineWorker_T <|-- HomeSearchProcessingWorker
    PipelineWorker_T --> PipelineRetryOptions : uses
```

**Base class provides:**
- **ExecuteAsync** — channel read loop
- **ExecuteWithRetryAsync** — exponential backoff, preserves context between retries, logs step summary
- **RunStepAsync** — checkpoint/resume per step (skips Completed, resumes PartiallyCompleted, records timing, appends to ErrorHistory)
- **OnPermanentFailureAsync** — dead letter hook after all retries exhausted
- **FormatStepSummary** — `"enrich:Completed(23401ms), notify:Failed(90123ms)"`

**Retry behavior (configurable via appsettings):**

```
Pipeline:Retry:
  MaxRetries: 3
  BaseDelaySeconds: 30
  MaxDelaySeconds: 600
  BackoffMultiplier: 2.0

Attempt 1: immediate
Attempt 2: 30s delay
Attempt 3: 60s delay
Attempt 4: 120s delay
→ Dead letter
```

The context is preserved across retries. On retry, `RunStepAsync` skips completed steps and resumes partially completed ones. Within a partially completed step, the action checks `ctx.HasCompletedSubStep()` to skip expensive sub-work.

### 3. Step and Sub-Step Tracking

**Steps** are named pipeline stages (e.g., `"enrich"`, `"draft-email"`, `"notify"`).

**Sub-steps** are checkpoints within a step for expensive operations:

```mermaid
flowchart LR
    subgraph "Step: enrich (Attempt 1 — partial failure)"
        S1["scrape-google<br/>8 ScraperAPI calls"]
        S2["call-claude<br/>469 in / 924 out tokens"]
        S3["save-file<br/>Write Research & Insights.md"]
        S1 -->|"done ✓"| S2
        S2 -->|"done ✓"| S3
        S3 -->|"FAIL ✗<br/>disk full"| X["PartiallyCompleted"]
    end

    style S1 fill:#2E7D32,color:#fff
    style S2 fill:#2E7D32,color:#fff
    style S3 fill:#D32F2F,color:#fff
    style X fill:#C8A951,color:#fff
```

```mermaid
flowchart LR
    subgraph "Step: enrich (Attempt 2 — resume)"
        R1["scrape-google<br/>SKIP ✓ (in context)"]
        R2["call-claude<br/>SKIP ✓ (in context)"]
        R3["save-file<br/>RETRY → success ✓"]
        R1 -->|"skip"| R2
        R2 -->|"skip"| R3
        R3 --> Done["Completed"]
    end

    style R1 fill:#C8A951,color:#fff
    style R2 fill:#C8A951,color:#fff
    style R3 fill:#2E7D32,color:#fff
    style Done fill:#2E7D32,color:#fff
```

On retry, `HasCompletedSubStep("enrich", "scrape-google")` → `true` → skip 8 API calls. Same for Claude. Only the file write retries. **Zero wasted tokens or credits.**

This is what saves money. A partial enrichment failure doesn't re-run Claude ($0.01+) or ScraperAPI (10 credits per rendered call).

### 4. Error History

Every failure is recorded with full context:

```csharp
public record ErrorEntry(
    int Attempt,
    DateTime Timestamp,
    string StepName,
    string Message,
    string? StackTrace);
```

On permanent failure, the dead letter contains the full error trail:

```json
{
  "correlationId": "abc-123",
  "totalFailures": 4,
  "pipelineDurationMs": 210000,
  "steps": {
    "enrich": { "status": "Completed", "durationMs": 23401 },
    "draft-email": { "status": "Completed", "durationMs": 12 },
    "notify": { "status": "Failed", "durationMs": 186000 }
  },
  "errorHistory": [
    { "attempt": 1, "step": "notify", "message": "gws: No such file or directory" },
    { "attempt": 2, "step": "notify", "message": "gws: No such file or directory" },
    { "attempt": 3, "step": "notify", "message": "gws: No such file or directory" },
    { "attempt": 4, "step": "notify", "message": "gws: No such file or directory" }
  ]
}
```

One glance: enrichment worked, email was drafted, notification failed 4 times because `gws` binary is missing.

### 5. IScraperClient — Centralized Scraper with Observability

All scraper calls go through a single `IScraperClient` instead of raw `HttpClient`:

```
IScraperClient
├── FetchAsync(targetUrl, source, agentId, ct): Task<string?>
└── IsAvailable: bool     ← false when rate-limited

ScraperClient implements IScraperClient
├── OTel counters: calls_total, calls_succeeded, calls_failed, calls_rate_limited, credits_used
├── OTel histogram: call_duration_ms (tagged by source)
├── Circuit breaker: sets IsAvailable=false on HTTP 429
├── Configurable via ScraperOptions (IOptions pattern)
└── Logs every call with source, agentId, response size, duration
```

**ScraperOptions (appsettings):**

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
      "realtor": "https://www.realtor.com/realestateandhomes-detail/{slug}",
      "google": "https://www.google.com/search?q={query}"
    }
  }
}
```

Swap a source URL without deploying code. Add a new comp source by adding a config entry.

**OTel counters (visible in Grafana):**

| Counter | Tags | Purpose |
|---------|------|---------|
| `scraper.calls_total` | source, agent_id | Total API calls |
| `scraper.calls_succeeded` | source | Successful fetches |
| `scraper.calls_failed` | source | Failed fetches (timeout, error) |
| `scraper.calls_rate_limited` | source | HTTP 429 responses |
| `scraper.credits_used` | — | Estimated credit consumption (render=10, plain=1) |
| `scraper.call_duration_ms` | source | Latency histogram |

### 6. Fan-Out at Endpoint

The endpoint dispatches to all three channels immediately after save + consent:

```csharp
// SubmitLeadEndpoint.Handle — after save + consent
await leadChannel.Writer.WriteAsync(new LeadProcessingRequest(agentId, lead, correlationId), ct);

if (lead.LeadType is LeadType.Seller or LeadType.Both && lead.SellerDetails is not null)
    await cmaChannel.Writer.WriteAsync(new CmaProcessingRequest(agentId, lead, correlationId), ct);

if (lead.LeadType is LeadType.Buyer or LeadType.Both && lead.BuyerDetails is not null)
    await homeSearchChannel.Writer.WriteAsync(new HomeSearchProcessingRequest(agentId, lead, correlationId), ct);
```

No chaining between workers. CMA doesn't wait for enrichment. Home search doesn't wait for CMA. Each runs its own pipeline independently.

**CmaProcessingRequest simplified** — `LeadEnrichment` and `LeadScore` removed:

```csharp
// Before
public sealed record CmaProcessingRequest(string AgentId, Lead Lead,
    LeadEnrichment Enrichment, LeadScore Score, string CorrelationId);

// After
public sealed record CmaProcessingRequest(string AgentId, Lead Lead, string CorrelationId);
```

`DetermineReportType` driven by comp count only (not lead score):

```csharp
internal static ReportType DetermineReportType(int compCount) =>
    compCount switch
    {
        >= 6 => ReportType.Comprehensive,
        >= 3 => ReportType.Standard,
        _ => ReportType.Lean
    };
```

---

## File Map

### New Files (Workers.Shared)

| File | Purpose |
|------|---------|
| `Workers.Shared/Context/PipelineContext.cs` | Base context + generic `PipelineContext<T>` |
| `Workers.Shared/Context/PipelineStepStatus.cs` | Status enum |
| `Workers.Shared/Context/StepRecord.cs` | Per-step timing, status, sub-steps, error history |
| `Workers.Shared/Context/ErrorEntry.cs` | Single error record |
| `Workers.Shared/PipelineWorker.cs` | Base class with retry, checkpoint, timing |
| `Workers.Shared/PipelineRetryOptions.cs` | Configurable retry policy |

### New Files (Clients.Scraper)

| File | Purpose |
|------|---------|
| `Clients.Scraper/ScraperClient.cs` | Centralized client with OTel + circuit breaker |
| `Clients.Scraper/ScraperOptions.cs` | Config: URLs, timeouts, limits |
| `Clients.Scraper/ScraperDiagnostics.cs` | OTel counters + histograms |

### New Files (Domain)

| File | Purpose |
|------|---------|
| `Domain/Shared/Interfaces/External/IScraperClient.cs` | Interface |

### New Files (Per-Pipeline Contexts)

| File | Purpose |
|------|---------|
| `Workers.Leads/LeadPipelineContext.cs` | Typed context with Enrichment, Score, Draft |
| `Workers.Cma/CmaPipelineContext.cs` | Typed context with Comps, Analysis, PDF |
| `Workers.HomeSearch/HomeSearchPipelineContext.cs` | Typed context with Listings |

### Modified Files

| File | Change |
|------|--------|
| `Workers.Leads/LeadProcessingWorker.cs` | Inherit `PipelineWorker`, use context + `RunStepAsync` |
| `Workers.Cma/CmaProcessingWorker.cs` | Inherit `PipelineWorker`, use context + `RunStepAsync` |
| `Workers.HomeSearch/HomeSearchProcessingWorker.cs` | Inherit `PipelineWorker`, use context + `RunStepAsync` |
| `Workers.Leads/ScraperLeadEnricher.cs` | Use `IScraperClient` |
| `Workers.Cma/ScraperCompSource.cs` | Use `IScraperClient` |
| `Workers.HomeSearch/ScraperHomeSearchProvider.cs` | Use `IScraperClient` |
| `Workers.Cma/CmaProcessingChannel.cs` | Remove Enrichment/Score from request |
| `Api/Program.cs` | Register IScraperClient, ScraperOptions, PipelineRetryOptions |
| `Api/appsettings.json` | Add Scraper + Pipeline:Retry config sections |
| `Api/Features/Leads/Submit/SubmitLeadEndpoint.cs` | Fan-out to all 3 channels |
| `Api/Diagnostics/OpenTelemetryExtensions.cs` | Add ScraperDiagnostics meter |

---

## Data Flow Example — Seller Lead (Happy Path)

```mermaid
sequenceDiagram
    actor User
    participant EP as API Endpoint
    participant LC as LeadChannel
    participant CC as CmaChannel
    participant LW as LeadWorker
    participant CW as CmaWorker
    participant Scraper as IScraperClient
    participant Claude as Claude API
    participant Disk as File Storage
    participant DL as Dead Letter

    User->>EP: Submit form
    EP->>EP: Validate + Save Lead Profile.md
    EP->>EP: Record consent (triple-write)

    par Fan-out (simultaneous)
        EP->>LC: Write(lead)
        EP->>CC: Write(lead)
    end

    EP-->>User: 202 Accepted

    par LeadWorker (independent)
        LC->>LW: Dequeue lead
        Note over LW: ctx = LeadPipelineContext

        rect rgb(230, 245, 230)
            Note over LW: RunStepAsync("enrich") — 23400ms
            LW->>Scraper: 8 Google searches
            Scraper-->>LW: HTML results
            Note over LW: ctx.MarkSubStepCompleted("enrich", "scrape")
            LW->>Claude: Enrich (469 in / 924 out tokens)
            Claude-->>LW: Enrichment + Score: 62
            Note over LW: ctx.MarkSubStepCompleted("enrich", "call-claude")
            LW->>Disk: Save Research & Insights.md
            Note over LW: ctx.MarkSubStepCompleted("enrich", "save")
        end

        rect rgb(230, 245, 230)
            Note over LW: RunStepAsync("draft-email") — 12ms
            LW->>Disk: Save Notification Draft.md
        end

        rect rgb(255, 230, 230)
            Note over LW: RunStepAsync("notify") — FAIL
            LW->>LW: gws CLI not found
            Note over LW: Retry 2 (30s): enrich SKIP, draft SKIP, notify FAIL
            Note over LW: Retry 3 (60s): notify FAIL
            Note over LW: Retry 4 (120s): notify FAIL
            LW->>DL: Permanent failure → dead letter JSON
        end

    and CmaWorker (independent, parallel)
        CC->>CW: Dequeue lead
        Note over CW: ctx = CmaPipelineContext

        rect rgb(255, 245, 230)
            Note over CW: RunStepAsync("fetch-comps") — 350ms
            CW->>Scraper: Zillow, Redfin, Realtor
            Scraper-->>CW: 429 Rate Limited
            Note over CW: Circuit breaker tripped — 0 comps
        end

        Note over CW: No comps → skip analyze + PDF
    end
```

---

## Dependency Rules

The new files follow the existing project dependency graph:

```mermaid
flowchart BT
    Domain["Domain<br/><i>IScraperClient, models, interfaces</i>"]
    Scraper["Clients.Scraper<br/><i>ScraperClient, Options, Diagnostics</i>"]
    Shared["Workers.Shared<br/><i>PipelineWorker, PipelineContext, StepRecord</i>"]
    Workers["Workers.*<br/><i>Lead, CMA, HomeSearch workers</i>"]
    Api["Api<br/><i>DI wiring, endpoints</i>"]

    Scraper --> Domain
    Shared --> Domain
    Workers --> Domain
    Workers --> Shared
    Api --> Domain
    Api --> Scraper
    Api --> Shared
    Api --> Workers

    style Domain fill:#C8A951,color:#fff
    style Scraper fill:#2E7D32,color:#fff
    style Shared fill:#4A90D9,color:#fff
    style Workers fill:#4A90D9,color:#fff
    style Api fill:#7B68EE,color:#fff
```

No new project-to-project dependencies. Architecture tests in `RealEstateStar.Architecture.Tests` continue to enforce this.

---

## Config Reference

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
      "realtor": "https://www.realtor.com/realestateandhomes-detail/{slug}",
      "google": "https://www.google.com/search?q={query}"
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

---

## Out of Scope

- **Persistent storage migration** (GDrive / Azure Blob) — separate spec
- **gws CLI installation in Docker** — separate task
- **Grafana dashboard creation** — OTel counters flow automatically once configured
- **Google Maps PlaceAutocompleteElement migration** — frontend task
- **WhatsApp worker** — different pattern (queue-based, not channel-based), not migrated to PipelineWorker
