# Azure Durable Functions Migration Plan

**Status:** Draft — awaiting review
**Date:** 2026-03-31
**Author:** Eddie + Claude

## Context

The API currently runs as a single Azure Container App with all pipelines (Activation, Lead, CMA, WhatsApp) running as `BackgroundService` hosted services in-process. This works but has limitations:

- Custom checkpoint/resume logic is error-prone and hand-rolled per pipeline
- No built-in audit trail for failed steps
- Scale-to-zero on Container Apps kills in-flight background work
- All pipelines share CPU/memory — a heavy CMA generation starves lead processing
- No cross-container durability for activation state (lives in file storage)

Azure Durable Functions provides orchestration-as-a-service: automatic checkpointing, retry policies, execution history, and independent scaling — but only the **Orchestrators** need it. Everything else stays what it is.

## Key Principle: Our Architecture Doesn't Change

The Durable Task framework is an **implementation detail of how Orchestrators checkpoint and dispatch**. It does NOT restructure our call hierarchy:

```mermaid
graph TD
    O["Orchestrator\n(Durable Function — checkpoint/replay)"]
    W["Workers\n(Regular Azure Function — pure compute)"]
    A["Activities\n(Regular Azure Function — compute + persist)"]
    S["Services\n(Regular Azure Function or in-process — sync logic)"]
    DS["DataServices\n(called by Activities/Services — storage routing)"]
    D["Data / Clients\n(called by Workers/Services — raw I/O)"]

    O -->|dispatches| W
    O -->|calls| A
    O -->|calls| S
    A -->|calls| S
    A -->|calls| DS
    S -->|calls| DS
    S -->|calls| D
    W -->|calls| D
    DS -->|calls| D

    style O fill:#7c3aed,color:#fff
    style W fill:#2563eb,color:#fff
    style A fill:#059669,color:#fff
    style S fill:#d97706,color:#fff
```

**Only Orchestrators become Durable Functions.** Workers, Activities, and Services become regular Azure Functions (or stay in-process calls). Same names, same roles, same dependency rules.

### File Storage Is Unchanged

The `IFileStorageProvider` → `DataServices` → `GDriveClient` / `LocalStorageProvider` chain is untouched. Lead files, CMA PDFs, and agent config files continue to be stored in the agent's Google Drive (prod) or local filesystem (dev).

```mermaid
flowchart LR
    subgraph "Durable (checkpoint/replay)"
        O["Orchestrator"]
    end

    subgraph "Regular Functions (no durability needed)"
        A["Activity"]
        S["Service"]
    end

    subgraph "Unchanged — not Azure Functions"
        DS["DataServices\n(storage routing)"]
        GD["GDriveClient\n(agent's Google Drive)"]
        LP["LocalStorageProvider\n(dev fallback)"]
        AB["Azure Blob\n(CMA PDFs)"]
    end

    O -->|"calls"| A
    O -->|"calls"| S
    A -->|"calls"| DS
    S -->|"calls"| DS
    DS -->|"prod"| GD
    DS -->|"dev"| LP
    DS -->|"blobs"| AB

    style O fill:#7c3aed,color:#fff
    style DS fill:#374151,color:#f9fafb
    style GD fill:#374151,color:#f9fafb
    style LP fill:#374151,color:#f9fafb
    style AB fill:#374151,color:#f9fafb
```

**Rule:** Orchestrators MUST NOT call DataServices, Clients, or any I/O directly. This is already enforced by our architecture — the migration doesn't change it. Durable orchestrator code must be deterministic (replay-safe), which means no I/O. Our separation already guarantees this.

## Current Architecture

```mermaid
graph TD
    subgraph "Azure Container App (single process)"
        API["ASP.NET Core API\n(HTTP endpoints + SignalR)"]
        AO["ActivationOrchestrator\n(BackgroundService)"]
        LO["LeadOrchestrator\n(BackgroundService)"]
        CMA["CmaProcessingWorker\n(PipelineWorker via Channel)"]
        HS["HomeSearchWorker\n(PipelineWorker via Channel)"]
        WH["WebhookProcessor\n(BackgroundService)"]
        TE["TrialExpiryService\n(BackgroundService)"]
    end

    AQ["Azure Queue\nactivation-requests"] --> AO
    LQ["Azure Queue\nlead-requests"] --> LO
    WQ["Azure Queue\nwhatsapp-webhooks"] --> WH

    LO -->|"Channel T"| CMA
    LO -->|"Channel T"| HS

    AO --> Claude["Anthropic Claude"]
    AO --> GWS["Google Workspace"]
    CMA --> RentCast["RentCast API"]
    CMA --> Blob["Azure Blob Storage"]
    LO --> Gmail["Gmail"]
```

## Proposed Architecture

```mermaid
graph TD
    subgraph "Azure Container App (API only)"
        API["ASP.NET Core API\n(HTTP endpoints + SignalR)"]
        TE["TrialExpiryService"]
    end

    subgraph "Azure Functions (Flex Consumption)"
        subgraph "Activation Pipeline"
            AO2["ActivationOrchestrator\n(Durable Orchestrator)"]
            AD["AgentDiscoveryWorker\n(Function — pure compute)"]
            VE["VoiceExtractionWorker\n(Function — pure compute)"]
            BD["BrandingDiscoveryWorker\n(Function — pure compute)"]
            BM["BrandMergeService\n(Function — sync logic)"]
            PP["PersistProfileActivity\n(Function — compute + persist)"]
            WN["WelcomeNotificationService\n(Function — sync logic)"]
        end

        subgraph "Lead Pipeline"
            LO2["LeadOrchestrator\n(Durable Orchestrator)"]
            CMA["CmaProcessingWorker\n(Function — pure compute)"]
            HS["HomeSearchWorker\n(Function — pure compute)"]
            NF["AgentNotifierService\n(Function — sync logic)"]
        end

        subgraph "WhatsApp"
            WH2["WebhookProcessor\n(Queue-triggered Function)"]
        end
    end

    API -->|"Queue message"| AO2
    API -->|"Queue message"| LO2
    API -->|"Queue message"| WH2

    AO2 -->|dispatches| AD
    AO2 -->|dispatches| VE
    AO2 -->|dispatches| BD
    AO2 -->|calls| BM
    AO2 -->|calls| PP
    AO2 -->|calls| WN

    LO2 -->|dispatches| CMA
    LO2 -->|dispatches| HS
    LO2 -->|calls| NF

    AD --> Claude["Anthropic Claude"]
    BD --> GWS["Google Workspace"]
    CMA --> RentCast["RentCast API"]
    PP --> Blob["Azure Blob Storage"]
    NF --> Gmail["Gmail"]
```

## What Moves vs. What Stays

| Component | Current | Proposed | Role Unchanged? |
|-----------|---------|----------|:---:|
| **HTTP API** | Container App | Container App | Yes |
| **SignalR Hub** | Container App | Container App | Yes |
| **TrialExpiryService** | BackgroundService | BackgroundService (stays) | Yes |
| **ActivationOrchestrator** | BackgroundService + queue poll | Durable Function | Yes — still orchestrates |
| **LeadOrchestrator** | BackgroundService + Channel\<T\> | Durable Function | Yes — still orchestrates |
| **CmaProcessingWorker** | PipelineWorker via Channel | Regular Azure Function | Yes — still pure compute |
| **HomeSearchWorker** | PipelineWorker via Channel | Regular Azure Function | Yes — still pure compute |
| **BrandMergeService** | In-process service | Regular Azure Function | Yes — still sync logic |
| **PersistProfileActivity** | In-process activity | Regular Azure Function | Yes — still compute + persist |
| **WebhookProcessor** | BackgroundService + queue poll | Queue-triggered Function | Yes — still processes messages |

**No renames. No restructuring. Same architecture, different runtime.**

## What the Durable Task Framework Replaces

```mermaid
graph LR
    subgraph "We Delete (hand-rolled)"
        A["Checkpoint JSON files"]
        B["PipelineWorker retry loops"]
        C["Queue polling loops"]
        D["Channel T coordination"]
        E["Visibility timeout hacks"]
    end

    subgraph "Framework Provides"
        F["Automatic checkpoint/replay"]
        G["Declarative retry policies"]
        H["Queue trigger binding"]
        I["Task.WhenAll fan-out"]
        J["30min execution timeout"]
    end

    A -->|replaced by| F
    B -->|replaced by| G
    C -->|replaced by| H
    D -->|replaced by| I
    E -->|replaced by| J
```

## Durable Functions Hosting: Flex Consumption

```mermaid
flowchart LR
    subgraph "Why Flex Consumption"
        A["Scale to zero\n(pay nothing idle)"]
        B["Per-function scaling\n(CMA scales independently)"]
        C["VNet support\n(access Azure Storage privately)"]
        D["Up to 30min execution\n(enough for CMA)"]
    end

    subgraph "vs. Consumption Plan"
        E["10min timeout\n(CMA can take 15min)"]
        F["No VNet"]
    end

    subgraph "vs. Dedicated Plan"
        G["Always-on billing\n(overkill for side project)"]
    end
```

## Activation Pipeline: Before & After

### Before (Custom Orchestration)

```mermaid
sequenceDiagram
    participant Q as Azure Queue
    participant O as ActivationOrchestrator
    participant FS as File Storage
    participant W as Workers/Services

    Q->>O: Dequeue activation request
    O->>FS: Check for existing checkpoint
    alt Checkpoint exists
        O->>O: Resume from last phase
    end

    loop Phase 1-5
        O->>W: Dispatch workers (inline)
        W-->>O: Return results
        O->>FS: Save checkpoint JSON
    end
    O->>Q: Delete message (complete)
```

### After (Durable Orchestrator)

```mermaid
sequenceDiagram
    participant Q as Azure Queue
    participant O as ActivationOrchestrator
    participant F as Durable Framework
    participant W as Workers/Activities/Services

    Q->>O: Trigger orchestration
    Note over O,F: Framework manages checkpoints automatically

    O->>W: Dispatch AgentDiscoveryWorker
    W-->>F: Result persisted to Table Storage
    F-->>O: Replay with result

    O->>W: Dispatch VoiceExtractionWorker
    W-->>F: Result persisted
    F-->>O: Replay with result

    O->>W: Call BrandMergeService
    Note over W: If crashes here, framework<br/>retries from this point automatically

    O->>W: Call PersistProfileActivity
    O->>W: Call WelcomeNotificationService
    Note over O: Orchestration complete —<br/>full history in Table Storage
```

## Lead Pipeline: Fan-Out Pattern

```mermaid
flowchart TD
    LO["LeadOrchestrator\n(Durable)"] --> Enrich["EnrichWorker\n(pure compute)"]
    Enrich --> Decision{"Lead Type?"}

    Decision -->|Seller| FanOut1["Task.WhenAll"]
    Decision -->|Buyer| FanOut2["Task.WhenAll"]
    Decision -->|Both| FanOut3["Task.WhenAll"]

    FanOut1 --> CMA["CmaProcessingWorker"]
    FanOut1 --> Notify1["AgentNotifierService"]

    FanOut2 --> HomeSearch["HomeSearchWorker"]
    FanOut2 --> Notify2["AgentNotifierService"]

    FanOut3 --> CMA2["CmaProcessingWorker"]
    FanOut3 --> HS2["HomeSearchWorker"]
    FanOut3 --> Notify3["AgentNotifierService"]

    CMA --> PdfAct["PdfActivity\n(compute + persist)"]
    PdfAct --> EmailSvc["LeadCommunicatorService"]
```

## Migration Phases

### Phase 0: Preparation (no behavior change)

- Ensure all Workers are stateless: input DTO in, output DTO out, no shared state
- Ensure all Activities/Services have clean interfaces (they already do via Domain)
- Add `Microsoft.Azure.Functions.Worker` and `Microsoft.DurableTask` packages
- Create `apps/api/RealEstateStar.Functions/` project as a thin host — it references the existing Worker/Activity/Service projects, not duplicates them

### Phase 1: WhatsApp Webhook (simplest, low risk)

- Queue-triggered function replaces `WebhookProcessorWorker`
- Drop custom polling loop and poison message counter
- Same `IConversationHandler` logic, just triggered by Azure Functions queue binding
- **Risk:** Low — stateless, single message processing

### Phase 2: Activation Pipeline (medium risk)

- `ActivationOrchestrator` becomes a Durable Function orchestrator
- Workers (AgentDiscovery, VoiceExtraction, etc.) become regular Azure Functions
- Activities (PersistProfile) and Services (BrandMerge, WelcomeNotification) become regular Azure Functions
- Delete custom checkpoint/resume JSON logic — framework handles it
- **Risk:** Medium — complex 5-phase orchestration, needs integration testing

### Phase 3: Lead Pipeline (medium-high risk)

- `LeadOrchestrator` becomes a Durable Function orchestrator
- CmaProcessingWorker and HomeSearchWorker become regular Azure Functions
- `Channel<T>` coordination replaced by `Task.WhenAll` fan-out in the orchestrator
- SignalR progress via HTTP polling (Durable Functions provides built-in status endpoint)
- **Risk:** Medium-high — loss of Channel\<T\> backpressure, needs load testing

### Phase 4: Decommission Container App workers

- Remove `BackgroundService` registrations from Program.cs
- Remove `PipelineWorker<T>` base class and `ProcessingChannelBase`
- Container App becomes API-only (HTTP + SignalR)
- Scale settings can be more aggressive (no background work to protect)

## Cost Comparison

```mermaid
graph LR
    subgraph "Current: Container App"
        CA["0.25 vCPU + 0.5 GiB\nAlways running when active\n~15-30 per mo at low traffic"]
    end

    subgraph "Proposed: Container App + Flex Functions"
        CA2["Container App\n(API only, lighter)\n~5-10 per mo"]
        FF["Flex Consumption\n(pay per execution)\n~2-5 per mo at low traffic"]
    end

    CA -->|"Migration"| CA2
    CA -->|"Migration"| FF
```

At current traffic (side project, single agent), Flex Consumption would cost pennies. The Container App gets lighter because it only serves HTTP.

## SignalR Considerations

The CMA pipeline currently pushes real-time progress via SignalR. Azure Functions can't hold WebSocket connections. Options:

```mermaid
flowchart TD
    A["Worker/Activity completes a step"] --> B{"How to notify client?"}

    B --> C["Option 1: HTTP Polling\nClient polls GET /status/instanceId\nDurable Functions provides this built-in"]
    B --> D["Option 2: Azure SignalR Service\nFunction posts to SignalR Service\nClients connect to managed hub"]
    B --> E["Option 3: Durable Entity\nEntity holds progress state\nClient queries entity"]

    C --> F["Simplest — works today\nSmall latency increase"]
    D --> G["Best UX — real-time\nAdds Azure SignalR cost"]
    E --> H["Clever but complex\nOverkill for progress bars"]

    style F fill:#10b981,color:#fff
```

**Recommendation:** Start with HTTP polling (Option 1). Durable Functions provides a built-in status query endpoint. Add Azure SignalR Service later if real-time UX matters enough.

## What This Unlocks

1. **Automatic checkpoint/replay** — crash mid-pipeline, resume exactly where you left off
2. **Declarative retry policies** — no more hand-rolled retry loops
3. **Execution history** — every dispatch/call logged in Table Storage (free audit trail)
4. **Independent scaling** — CMA worker scales separately from lead intake
5. **30min execution** — Flex Consumption supports it natively (vs. our 5min visibility timeout hack)
6. **Simpler testing** — Workers are already pure compute; now they're independently deployable too
7. **Cost optimization** — pay per execution, not per always-on container

## Idempotency: The Replay Problem

With Durable Functions, the orchestrator and the functions it calls run in **separate executions**. If a function completes successfully but the orchestrator crashes before recording that completion, the framework replays and calls the function again. This is safe for pure compute and file overwrites — but dangerous for side effects like sending emails.

```mermaid
sequenceDiagram
    participant O as Orchestrator
    participant F as Framework
    participant A as Activity/Service
    participant E as External (Gmail, WhatsApp)

    O->>A: Call PersistProfileActivity
    A->>A: Write file to blob storage
    A-->>F: Return success
    Note over F: Framework crashes BEFORE<br/>persisting completion to Table Storage

    F->>F: Restart + replay
    O->>A: Call PersistProfileActivity (again)
    A->>A: Write same file to blob (idempotent — safe)
    A-->>F: Return success
    F->>F: Completion persisted

    O->>A: Call LeadCommunicatorService
    A->>E: Send email via Gmail
    A-->>F: Return success
    Note over F: Framework crashes again

    F->>F: Restart + replay
    O->>A: Call LeadCommunicatorService (again)
    A->>E: Send DUPLICATE email via Gmail
    Note over E: Recipient gets two identical emails
```

### Idempotency Audit

```mermaid
flowchart TD
    subgraph "Safe on Replay (idempotent)"
        W["Workers\n(pure compute, no side effects)"]
        PP["PersistProfileActivity\n(overwrites same file path)"]
        BW["Blob writes — CMA PDF\n(deterministic blob name)"]
        DS["DataServices writes\n(upsert semantics)"]
    end

    subgraph "Unsafe on Replay (NOT idempotent)"
        GM["Gmail send\n(duplicate email)"]
        WA["WhatsApp send\n(duplicate message)"]
        ST["Stripe charges\n(duplicate charge without idempotency key)"]
    end

    subgraph "Fix: Idempotency Guards"
        G1["Check-before-send\nQuery 'sent' flag in storage"]
        G2["Idempotency key\nPass orchestration instance ID\nas dedup key to external API"]
    end

    GM --> G1
    GM --> G2
    WA --> G1
    WA --> G2
    ST --> G2

    style W fill:#059669,color:#fff
    style PP fill:#059669,color:#fff
    style BW fill:#059669,color:#fff
    style DS fill:#059669,color:#fff
    style GM fill:#991b1b,color:#fff
    style WA fill:#991b1b,color:#fff
    style ST fill:#991b1b,color:#fff
    style G1 fill:#2563eb,color:#fff
    style G2 fill:#2563eb,color:#fff
```

### Mitigation Strategy

Every Service that calls an external API with non-idempotent side effects needs a guard before the migration:

1. **Gmail / Email sends** — Before sending, check a `notification_sent` flag in lead storage (DataServices). The orchestrator's deterministic instance ID (`{accountId}-{leadId}`) serves as the dedup key. If the flag exists, skip the send.

2. **WhatsApp sends** — Same pattern: check `whatsapp_sent` flag before dispatching. WhatsApp Business API also supports idempotency via `messaging_product` + `to` + dedup window.

3. **Stripe calls** — Already supports idempotency keys natively. Pass the Durable Functions instance ID as the `Idempotency-Key` header.

4. **File/Blob writes** — Already idempotent (deterministic paths, overwrite semantics). No change needed.

5. **Workers** — Pure compute, no side effects. No change needed.

This is good practice regardless of Azure Functions — the current in-process model is only "safe" because crashes are rare, not because the code is idempotent.

## Resiliency Gap Analysis

Every resiliency pattern currently in the codebase must be preserved or improved. This section audits each pattern against the Durable Functions migration.

### Pattern-by-Pattern Assessment

```mermaid
flowchart TD
    subgraph "Preserved or Improved"
        R["Retry with Backoff\n(DF retry policies replace\nPipelineRetryOptions)"]
        CP["Checkpoint/Resume\n(DF replay replaces\ncustom JSON checkpoints)"]
        DL["Dead Letter\n(DF max retry → failure handler\nreplaces OnPermanentFailureAsync)"]
        EI["Error Isolation\n(Functions run independently\nsame as separate HostedServices)"]
        GS["Graceful Shutdown\n(Functions handle cancellation\nnatively via CancellationToken)"]
        OB["Observability\n(OTel works in Azure Functions\nActivitySource + metrics carry over)"]
        AH["Audit/Error History\n(IMPROVED: DF execution history\nin Table Storage replaces\nin-memory ErrorEntry lists)"]
    end

    subgraph "At Risk — Needs Mitigation"
        CB["Circuit Breakers\n(Polly HTTP policies)"]
        BP["Backpressure\n(Channel T bounded wait)"]
        DD["Deduplication\n(In-memory IContentCache)"]
        TO["Timeouts\n(Worker collection timeout)"]
        PC["Partial Completion\n(One worker fails, pipeline continues)"]
        HC["Health Checks\n(BackgroundServiceHealthTracker)"]
    end

    style R fill:#059669,color:#fff
    style CP fill:#059669,color:#fff
    style DL fill:#059669,color:#fff
    style EI fill:#059669,color:#fff
    style GS fill:#059669,color:#fff
    style OB fill:#059669,color:#fff
    style AH fill:#059669,color:#fff
    style CB fill:#d97706,color:#fff
    style BP fill:#d97706,color:#fff
    style DD fill:#d97706,color:#fff
    style TO fill:#d97706,color:#fff
    style PC fill:#d97706,color:#fff
    style HC fill:#d97706,color:#fff
```

### Detailed Risk Assessment

| Pattern | Current Implementation | DF Equivalent | Risk | Mitigation |
|---------|----------------------|---------------|:----:|------------|
| **Retry (pipeline)** | `PipelineWorker` exponential backoff: 3x, 30s base, 600s max, 2x multiplier | DF `RetryPolicy` — same params, declarative | None | Map existing `PipelineRetryOptions` to DF retry config |
| **Retry (HTTP)** | Polly policies per client: Claude 3x/2s, Scraper 2x/1s, GWS 3x/1s, RentCast 1x/5s | Same — Polly runs inside Functions | None | HttpClientFactory + Polly works identically in Functions |
| **Circuit Breakers** | Polly HTTP CBs: 5 per external service, FailureRatio=1.0, break durations 30s-120s | Same — Polly runs inside Functions | **Low** | CB state is per-process. With scale-to-zero, CB state resets on cold start. Same behavior as Container App restart. |
| **Checkpoint/Resume** | Custom JSON files in file storage + `LeadRetryState` content hashing | DF automatic replay from Table Storage | **Improved** | Delete custom checkpoint code. DF replay is more reliable. |
| **Dead Letter** | `OnPermanentFailureAsync` after max retries; WhatsApp poison after 5 dequeues | DF `MaxNumberOfAttempts` → calls failure handler | None | Map `OnPermanentFailureAsync` to DF's `OnFailure` callback |
| **Backpressure** | `Channel<T>` bounded (50-100 capacity), `BoundedChannelFullMode.Wait` | **No direct equivalent** | **Medium** | See mitigation below |
| **Deduplication** | `IContentCache` in-memory (CMA 24h TTL, HS 1h TTL) + `LeadRetryState` per-lead hashing | **No direct equivalent** | **Medium** | See mitigation below |
| **Graceful Shutdown** | `OperationCanceledException` rethrow pattern, `ReadAllAsync` cancellation | DF passes `CancellationToken` to all function invocations | None | Same pattern, different trigger |
| **Timeouts** | Worker collection: 5min default, PDF: inherited async, Queue visibility: 30s | DF orchestrator timeout via `Task.WhenAll().WaitAsync()` or `CreateTimer` | **Low** | Reimplement timeout in orchestrator using `ctx.CreateTimer()` + `Task.WhenAny()` |
| **Partial Completion** | LeadOrchestrator continues if one worker times out, records partial result | DF `Task.WhenAll` throws on any failure by default | **Medium** | See mitigation below |
| **Error Isolation** | Separate `BackgroundService` per worker, per-lead try/catch, `RunSafeAsync` | Separate function invocations per worker | **Improved** | Functions are process-isolated by default |
| **Health Checks** | `BackgroundServiceHealthTracker` + 5min staleness threshold, queue depth reporting | **No direct equivalent in Functions** | **Medium** | See mitigation below |
| **Observability** | `ActivitySource` spans, OTel metrics, correlation IDs, structured log codes, `ErrorEntry` lists | OTel works in Functions. `ErrorEntry` replaced by DF execution history. | **Improved** | DF execution history is persistent (Table Storage), unlike in-memory `ErrorEntry` |

### Mitigations for At-Risk Patterns

#### Backpressure (Medium Risk)

**Current:** Bounded `Channel<T>` with `Wait` policy — writers block when 50 items queued, preventing the orchestrator from overwhelming downstream workers.

**Problem:** Durable Functions dispatches work via Azure Storage queues, not in-memory channels. No built-in backpressure.

**Mitigation:**
```mermaid
flowchart TD
    O["LeadOrchestrator\n(Durable)"] --> Check{"Active CMA\nworkers < 50?"}
    Check -->|Yes| Dispatch["Dispatch CmaProcessingWorker"]
    Check -->|No| Wait["ctx.CreateTimer(30s)\nthen re-check"]
    Wait --> Check

    Dispatch --> Complete["Worker completes"]
    Complete --> O
```

Use a **Durable Entity** as a semaphore counter. Before dispatching, the orchestrator checks the entity's count. If at capacity, it waits with `ctx.CreateTimer()`. This preserves the bounded-queue semantics without Channel\<T\>.

Alternatively: **accept the change**. Azure Queue Storage has its own backpressure (Functions scale based on queue depth). The 50-item Channel limit was chosen for in-process memory, not for correctness. With Functions, each invocation is isolated — no shared memory pressure.

#### Deduplication (Medium Risk)

**Current:** `IContentCache` (in-memory `MemoryCache`) deduplicates cross-lead CMA/HomeSearch work. Same property address within 24h → cached result, no re-computation.

**Problem:** In-memory cache doesn't survive across function invocations. Each function instance starts fresh.

**Mitigation:** Replace `MemoryContentCache` with a distributed cache:
- **Option A:** Azure Cache for Redis — fast, adds ~$15/mo cost
- **Option B:** Azure Table Storage — free (included with Functions storage account), slightly slower
- **Option C:** Durable Entity as cache — built-in, no extra cost, but limited to 64KB per entity

**Recommendation:** Option B (Table Storage). The cache is checked once per lead, not in a hot loop. Table Storage latency (~10ms) is acceptable. Zero additional cost.

`LeadRetryState` per-lead hashing is unaffected — it's passed as input to the orchestrator and stored in DF execution history.

#### Partial Completion (Medium Risk)

**Current:** LeadOrchestrator dispatches CMA + HomeSearch in parallel. If one times out, the other's result is still used. Pipeline continues with whatever completed.

**Problem:** DF `Task.WhenAll` throws `TaskFailedException` if any sub-task fails. Default behavior is all-or-nothing.

**Mitigation:** Use `Task.WhenAny` + individual try/catch instead of `Task.WhenAll`:

```csharp
// In Durable Orchestrator
var cmaTask = ctx.CallActivityAsync<CmaResult?>("CmaProcessingWorker", input);
var hsTask = ctx.CallActivityAsync<HomeSearchResult?>("HomeSearchWorker", input);

CmaResult? cmaResult = null;
HomeSearchResult? hsResult = null;

try { cmaResult = await cmaTask; } catch { /* log, continue */ }
try { hsResult = await hsTask; } catch { /* log, continue */ }

// Continue with whatever completed — same as current behavior
```

This preserves the exact same partial-completion semantics.

#### Health Checks (Medium Risk)

**Current:** `BackgroundServiceHealthTracker` monitors 4 workers via last-activity timestamps. If a worker hasn't processed in 5 minutes but has items queued, readiness probe reports Unhealthy.

**Problem:** Functions don't have a centralized health check endpoint with per-worker staleness tracking.

**Mitigation:**
- **Azure Functions built-in monitoring:** Functions runtime reports execution counts, failures, and duration to Azure Monitor automatically.
- **Durable Functions status API:** Query orchestration instances by status (Running, Failed, Suspended) via the built-in HTTP management API.
- **Custom health endpoint on the Container App:** The API (which stays on Container App) can query the DF status API to build the same health picture. Add a `/health/workers` endpoint that checks recent DF instance statuses.
- **Alert rules:** Azure Monitor alert on function failure rate > threshold, replacing the staleness check.

This is actually **more observable** than the current approach — DF tracks per-instance status, not just last-activity timestamps.

## Open Questions

1. **Shared DI:** Workers/Activities/Services need `IAnthropicClient`, `IGDriveClient`, etc. How to register DI in the Functions host while reusing existing service registrations from the API project?
2. **Local development:** Azurite for local queue/table emulation? Or keep in-memory fallback?
3. **Monitoring:** Keep existing OpenTelemetry setup or switch to Azure Monitor / Application Insights?
4. **Deployment:** Separate GitHub Actions workflow for Functions, or combined with API?
5. **Feature flags:** Roll out per-pipeline (WhatsApp first, then Activation, then Lead) or big bang?
