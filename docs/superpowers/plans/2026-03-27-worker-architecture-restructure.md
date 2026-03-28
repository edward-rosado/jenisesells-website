# Worker Architecture Restructure — Complete Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Decompose the monolith `Workers.Leads` project into single-responsibility projects: `Activities.Pdf`, `Services.AgentNotifier`, `Services.LeadCommunicator`, and `Workers.Lead.Orchestrator`. Add domain model improvements (Notes, SubmissionCount, LeadRetryState, CommunicationRecord, LeadPipelineContext, IDiagnosticsProvider), wire the new architecture with self-contained DI extensions, implement smart content-aware retry with cross-lead caching, and enforce the full dependency graph with exhaustive architecture tests.

**Architecture:** Per-lead orchestrator pattern. Each lead gets its own `LeadOrchestrator` instance that sequences: Score -> Fan-out (CMA + HomeSearch) -> PDF -> Lead Communication -> Agent Notification -> Persist. Sub-workers are stateless activities called directly (not channel-dispatched). Services (`AgentNotifier`, `LeadCommunicator`) are synchronous service calls, not BackgroundServices. PDF is a regular activity (not a channel-based worker).

**Tech Stack:** .NET 10, Channel\<T\>, QuestPDF, Claude API, Gmail API, WhatsApp Business API, SHA256, IMemoryCache, OpenTelemetry, xUnit, NetArchTest

**Spec:** `docs/superpowers/specs/2026-03-27-worker-architecture-restructure-design.md`

---

## Architecture Overview

```
Lead API endpoint
  |
  v
LeadOrchestratorChannel (BackgroundService reads from channel)
  |
  v
LeadOrchestrator instance (one per lead)
  |
  +--[1]--> Score (sync, pure logic — LeadScorer)
  |
  +--[2]--> Fan-out: CMA + HomeSearch in parallel (via channels)
  |         +-- CMA Activity (async — RentCast + Claude analysis)
  |         +-- HomeSearch Activity (async — scraper + Claude parsing)
  |
  +--[3]--> PDF Activity (async — QuestPDF → blob storage, direct call)
  |
  +--[4]--> LeadCommunicator (async — Claude draft → Gmail to lead)
  |
  +--[5]--> AgentNotifier (async — WhatsApp → Gmail fallback to agent)
  |
  +--[6]--> PersistActivity (batch upsert — lead profile, summaries, drafts, retry state)
```

### Project Dependency Diagram

```
Domain         → nothing
Workers.Shared → Domain
Activities.Pdf → Domain + Workers.Shared
Services.AgentNotifier → Domain + Workers.Shared
Services.LeadCommunicator → Domain + Workers.Shared
Workers.Lead.CMA → Domain + Workers.Shared
Workers.Lead.HomeSearch → Domain + Workers.Shared
Workers.Lead.Orchestrator → Domain + Workers.Shared
                          + Activities.Pdf
                          + Services.AgentNotifier
                          + Services.LeadCommunicator
                          + Workers.Lead.CMA
                          + Workers.Lead.HomeSearch
Api → everything (sole composition root)
```

---

## Spawn Pattern

```
Phase 1: Launch Tasks 1–7 in parallel (7 agents) — Domain model foundation (all independent)
Phase 2: Launch Tasks 8–11 in parallel (4 agents) — Create new projects (depend on Phase 1 models)
Phase 3: Launch Tasks 12–13 in parallel (2 agents) — Wire new architecture (depends on Phase 2 projects)
Phase 4: Launch Tasks 14–17 in parallel (4 agents) — Cleanup (depends on Phase 3 wiring)
Phase 5: Launch Tasks 18–22 in parallel (5 agents) — Smart retry + PersistActivity (depends on Phase 4)
Phase 6: Launch Tasks 23–25 in parallel (3 agents) — Exhaustive dependency tests (depends on Phase 5)
Post:    Launch Tasks 26–28 in parallel (3 agents) — Documentation + reviews
```

**Important codebase notes for implementers:**
- The current codebase uses `Activities.Pdf`, `Services.AgentNotifier`, `Services.LeadCommunicator` naming (not `Workers.Shared.Pdf` etc.) — the spec's `Workers.Shared.*` naming was a draft; implementation uses `Activities.*` and `Services.*` top-level groupings.
- `IDiagnosticsProvider` and `DiagnosticsSnapshot` already live in `Domain/Shared/Diagnostics/` — the spec proposed moving them to `Workers.Shared` but Domain is the correct location (interfaces belong in Domain).
- `ILeadCommunicationService` is the interface name for the lead communicator (not `ILeadCommunicator`).
- Worker test projects live under `RealEstateStar.Tests/RealEstateStar.Workers.Tests/` (grouped), not flat.
- Service test projects live under `RealEstateStar.Tests/RealEstateStar.Services.Tests/`.
- Activity test projects live under `RealEstateStar.Tests/RealEstateStar.Activities.Tests/`.
- WhatsApp interface is `IWhatsAppSender` at `Domain/Shared/Interfaces/Senders/IWhatsAppSender.cs`.
- `SellerDetails` uses `Beds`/`Baths`/`Sqft` (not `Bedrooms`/`Bathrooms`/`SquareFootage`).
- Each project already has its own `ServiceCollectionExtensions.cs` for self-contained DI registration.

---

## Phase 1: Domain Model Foundation (all independent — run Tasks 1–7 in parallel)

---

### Task 1: Add Notes property to SellerDetails and BuyerDetails

**Files:**
- Modify: `apps/api/RealEstateStar.Domain/Leads/Models/SellerDetails.cs`
- Modify: `apps/api/RealEstateStar.Domain/Leads/Models/BuyerDetails.cs`
- Modify: `apps/api/RealEstateStar.Api/Features/Leads/Submit/SubmitLeadEndpoint.cs`
- Test: `apps/api/RealEstateStar.Tests/RealEstateStar.Domain.Tests/Leads/SellerDetailsTests.cs`
- Test: `apps/api/RealEstateStar.Tests/RealEstateStar.Domain.Tests/Leads/BuyerDetailsTests.cs`

- [ ] **Step 1: Add `Notes` property to `SellerDetails`**

Add `public string? Notes { get; init; }` to the `SellerDetails` record. This allows sellers to provide freeform context ("Recently renovated kitchen", "Motivated seller, needs to close by June") that the CMA analyzer can factor into its pricing strategy.

- [ ] **Step 2: Add `Notes` property to `BuyerDetails`**

Add `public string? Notes { get; init; }` to the `BuyerDetails` record. This allows buyers to provide search context ("Must have garage and backyard", "Relocating from NYC") that HomeSearch can use for filtering.

- [ ] **Step 3: Map request.Notes into both detail models in SubmitLeadEndpoint**

The API request has a single `notes` field. SubmitLeadEndpoint maps it into BOTH models so each activity reads notes from its own model:

```csharp
if (lead.SellerDetails is not null)
    lead.SellerDetails = lead.SellerDetails with { Notes = request.Notes };
if (lead.BuyerDetails is not null)
    lead.BuyerDetails = lead.BuyerDetails with { Notes = request.Notes };
```

- [ ] **Step 4: Write tests for notes mapping**

Test that notes propagate correctly to SellerDetails and BuyerDetails. Verify CMA reads `ctx.Lead.SellerDetails.Notes` and HomeSearch reads `ctx.Lead.BuyerDetails.Notes`.

- [ ] **Step 5: Verify build + run tests**

```bash
dotnet build apps/api/RealEstateStar.Api.sln --no-restore
dotnet test apps/api/RealEstateStar.Api.sln --no-restore
```

- [ ] **Step 6: Commit**

```bash
git add apps/api/RealEstateStar.Domain/Leads/Models/SellerDetails.cs \
       apps/api/RealEstateStar.Domain/Leads/Models/BuyerDetails.cs \
       apps/api/RealEstateStar.Api/Features/Leads/Submit/SubmitLeadEndpoint.cs
git commit -m "feat: add Notes property to SellerDetails and BuyerDetails for activity-specific context"
```

---

### Task 2: Add SubmissionCount to Lead model

**Files:**
- Modify: `apps/api/RealEstateStar.Domain/Leads/Models/Lead.cs`
- Modify: `apps/api/RealEstateStar.Api/Features/Leads/Submit/SubmitLeadEndpoint.cs`
- Test: `apps/api/RealEstateStar.Tests/RealEstateStar.Api.Tests/Features/Leads/Submit/SubmitLeadEndpointTests.cs`

- [ ] **Step 1: Add `SubmissionCount` property to `Lead`**

```csharp
public int SubmissionCount { get; set; } = 1;
```

Repeat form submissions signal high intent. The LeadScorer uses this as a 10% weight factor.

- [ ] **Step 2: Increment SubmissionCount on dedup in SubmitLeadEndpoint**

When `GetByEmailAsync` finds an existing lead (same email = dedup), increment `SubmissionCount` before dispatching to the orchestrator.

- [ ] **Step 3: Write tests**

Test: first submission = 1, dedup'd resubmission = 2, third submission = 3.

- [ ] **Step 4: Verify build + run tests**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: add SubmissionCount to Lead — repeat submissions boost engagement score"
```

---

### Task 3: Create LeadRetryState model

**Files:**
- Create: `apps/api/RealEstateStar.Domain/Leads/Models/LeadRetryState.cs`
- Modify: `apps/api/RealEstateStar.Domain/Leads/Models/Lead.cs`
- Test: `apps/api/RealEstateStar.Tests/RealEstateStar.Domain.Tests/Leads/LeadRetryStateTests.cs`

- [ ] **Step 1: Create `LeadRetryState` record**

```csharp
namespace RealEstateStar.Domain.Leads.Models;

public record LeadRetryState
{
    /// Key: activity name ("cma", "homeSearch", "pdf", "draftLeadEmail", "draftAgentNotification")
    /// Value: SHA256 hash of the input that produced the completed result
    public Dictionary<string, string> CompletedActivityKeys { get; init; } = new();

    /// Key: activity name
    /// Value: storage path or serialized result reference
    public Dictionary<string, string> CompletedResultPaths { get; init; } = new();

    public string? GetHash(string activityName) =>
        CompletedActivityKeys.GetValueOrDefault(activityName);

    public bool IsCompleted(string activityName, string currentInputHash) =>
        CompletedActivityKeys.TryGetValue(activityName, out var storedHash)
        && storedHash == currentInputHash;
}
```

- [ ] **Step 2: Add `RetryState` property to `Lead`**

```csharp
public LeadRetryState? RetryState { get; set; }
```

- [ ] **Step 3: Write tests**

Test `IsCompleted` with matching hash (returns true), different hash (returns false), missing key (returns false). Test `GetHash` returns null for missing keys.

- [ ] **Step 4: Verify build + run tests**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: add LeadRetryState — content-aware retry tracking for orchestrator"
```

---

### Task 4: Create CommunicationRecord model

**Files:**
- Create: `apps/api/RealEstateStar.Domain/Leads/Models/CommunicationRecord.cs`
- Test: `apps/api/RealEstateStar.Tests/RealEstateStar.Domain.Tests/Leads/CommunicationRecordTests.cs`

- [ ] **Step 1: Create `CommunicationRecord` record**

```csharp
namespace RealEstateStar.Domain.Leads.Models;

public record CommunicationRecord
{
    public required string Subject { get; init; }
    public required string HtmlBody { get; init; }
    public required string Channel { get; init; }       // "email", "whatsapp", "email-fallback"
    public required DateTimeOffset DraftedAt { get; init; }
    public DateTimeOffset? SentAt { get; set; }
    public bool Sent { get; set; }
    public string? Error { get; set; }
    public required string ContentHash { get; init; }   // SHA256 of draft inputs — dedup key
}
```

- [ ] **Step 2: Write tests**

Test record creation, sent status mutation, content hash comparison for dedup.

- [ ] **Step 3: Verify build + run tests**

- [ ] **Step 4: Commit**

```bash
git commit -m "feat: add CommunicationRecord — draft + send tracking with content hash dedup"
```

---

### Task 5: Create LeadPipelineContext + LeadPipelineResult

**Files:**
- Create: `apps/api/RealEstateStar.Domain/Leads/Models/LeadPipelineContext.cs`
- Test: `apps/api/RealEstateStar.Tests/RealEstateStar.Domain.Tests/Leads/LeadPipelineContextTests.cs`

- [ ] **Step 1: Create `LeadPipelineContext` class**

Mutable context passed through all pipeline activities. Each activity reads what it needs and writes its output back. NOT thread-safe — all writes happen sequentially after parallel activities complete.

```csharp
namespace RealEstateStar.Domain.Leads.Models;

public class LeadPipelineContext
{
    // Input (set at creation)
    public required Lead Lead { get; init; }
    public required AgentNotificationConfig AgentConfig { get; init; }
    public required string CorrelationId { get; init; }
    public LeadRetryState RetryState { get; set; } = new();

    // Activity outputs (set by each activity)
    public LeadScore? Score { get; set; }
    public CmaWorkerResult? CmaResult { get; set; }
    public HomeSearchWorkerResult? HsResult { get; set; }
    public string? PdfStoragePath { get; set; }
    public CommunicationRecord? LeadEmail { get; set; }
    public CommunicationRecord? AgentNotification { get; set; }

    public LeadPipelineResult ToResult() => new(
        LeadId: Lead.Id.ToString(),
        Success: true,
        Score: Score,
        CmaResult: CmaResult,
        HsResult: HsResult,
        PdfStoragePath: PdfStoragePath,
        LeadEmailSent: LeadEmail?.Sent ?? false,
        AgentNotified: AgentNotification?.Sent ?? false);
}

public record LeadPipelineResult(
    string LeadId, bool Success, LeadScore? Score,
    CmaWorkerResult? CmaResult, HomeSearchWorkerResult? HsResult,
    string? PdfStoragePath, bool LeadEmailSent, bool AgentNotified);
```

- [ ] **Step 2: Write tests**

Test `ToResult()` mapping, default values, context accumulation across activities.

- [ ] **Step 3: Verify build + run tests**

- [ ] **Step 4: Commit**

```bash
git commit -m "feat: add LeadPipelineContext + LeadPipelineResult — shared state for orchestrator activities"
```

---

### Task 6: Create IDiagnosticsProvider + DiagnosticsSnapshot

**Files:**
- Create: `apps/api/RealEstateStar.Domain/Shared/Diagnostics/IDiagnosticsProvider.cs`
- Create: `apps/api/RealEstateStar.Domain/Shared/Diagnostics/DiagnosticsSnapshot.cs`

- [ ] **Step 1: Create `IDiagnosticsProvider` interface**

```csharp
namespace RealEstateStar.Domain.Shared.Diagnostics;

public interface IDiagnosticsProvider
{
    string ServiceName { get; }
    DiagnosticsSnapshot GetSnapshot();
}
```

- [ ] **Step 2: Create `DiagnosticsSnapshot` record**

```csharp
namespace RealEstateStar.Domain.Shared.Diagnostics;

public record DiagnosticsSnapshot(
    string ServiceName,
    Dictionary<string, long> Counters,
    Dictionary<string, double> Histograms,
    DateTime CollectedAt);
```

Each worker project will implement `IDiagnosticsProvider`. Auto-registration by scanning for implementations. A `/diagnostics` endpoint aggregates all providers into a single JSON response.

- [ ] **Step 3: Verify build**

```bash
dotnet build apps/api/RealEstateStar.Domain/RealEstateStar.Domain.csproj --no-restore
```

- [ ] **Step 4: Commit**

```bash
git commit -m "feat: add IDiagnosticsProvider + DiagnosticsSnapshot — shared diagnostics interface for worker projects"
```

---

### Task 7: Create ILeadCommunicationService interface

**Files:**
- Create: `apps/api/RealEstateStar.Domain/Leads/Interfaces/ILeadCommunicationService.cs`

- [ ] **Step 1: Create `ILeadCommunicationService` interface**

```csharp
namespace RealEstateStar.Domain.Leads.Interfaces;

public interface ILeadCommunicationService
{
    Task<CommunicationRecord> DraftAsync(LeadPipelineContext ctx, CancellationToken ct);
    Task<CommunicationRecord> SendAsync(CommunicationRecord draft, LeadPipelineContext ctx, CancellationToken ct);
}
```

This interface is separate from `IAgentNotifier` because it serves a different audience (the lead vs. the agent), uses different channels (email from agent's Gmail), and requires Claude for content generation.

- [ ] **Step 2: Verify build**

- [ ] **Step 3: Commit**

```bash
git commit -m "feat: add ILeadCommunicationService — draft + send interface for lead emails"
```

---

## Phase 2: Create New Projects (depend on Phase 1 — run Tasks 8–11 in parallel)

---

### Task 8: Create Activities.Pdf project (PdfActivity + CmaPdfGenerator)

**Files:**
- Create: `apps/api/RealEstateStar.Activities/RealEstateStar.Activities.Pdf/RealEstateStar.Activities.Pdf.csproj`
- Create: `apps/api/RealEstateStar.Activities/RealEstateStar.Activities.Pdf/PdfActivity.cs`
- Create: `apps/api/RealEstateStar.Activities/RealEstateStar.Activities.Pdf/CmaPdfGenerator.cs`
- Create: `apps/api/RealEstateStar.Activities/RealEstateStar.Activities.Pdf/PdfDiagnostics.cs`
- Create: `apps/api/RealEstateStar.Activities/RealEstateStar.Activities.Pdf/ServiceCollectionExtensions.cs`
- Create: `apps/api/RealEstateStar.Tests/RealEstateStar.Activities.Tests/RealEstateStar.Activities.Pdf.Tests/`
- Modify: `apps/api/RealEstateStar.Api.sln` — add new project

- [ ] **Step 1: Create project file**

Dependencies: `RealEstateStar.Domain` + `RealEstateStar.Workers.Shared` + QuestPDF NuGet. This is a regular activity — NOT a BackgroundService. No channel, no `PdfProcessingChannel`.

- [ ] **Step 2: Implement `PdfActivity`**

```csharp
public class PdfActivity(ICmaPdfGenerator generator, IDocumentStorageProvider storage)
{
    public async Task<string> ExecuteAsync(LeadPipelineContext ctx, CancellationToken ct)
    {
        var pdfBytes = generator.Generate(ctx.CmaResult, ctx.AgentConfig);
        var folder = LeadPaths.LeadFolder(ctx.Lead.FullName);
        var fileName = $"{ctx.Lead.Id}-CMA-Report.pdf.b64";
        await storage.WriteDocumentAsync(folder, fileName, Convert.ToBase64String(pdfBytes), ct);
        return $"{folder}/{fileName}";
    }
}
```

PDF bytes are 2-10MB and must NOT be held on the context. PdfActivity writes directly to storage and puts only the storage path on the context.

- [ ] **Step 3: Move `CmaPdfGenerator` from Workers.Leads**

Copy the QuestPDF-based CMA report renderer. Update namespace to `RealEstateStar.Activities.Pdf`.

- [ ] **Step 4: Add `PdfDiagnostics`**

Metrics: `pdf.generation_duration_ms`, `pdf.size_bytes`, `pdf.storage_duration_ms`, `pdf.success`, `pdf.failed`.

- [ ] **Step 5: Add `ServiceCollectionExtensions.AddPdfService()`**

Self-contained DI registration for the PDF activity and generator.

- [ ] **Step 6: Create test project + tests**

Test cases: PDF generation with comps, without comps, currency formatting, storage path construction, error handling.

- [ ] **Step 7: Verify build + run tests + coverage**

```bash
dotnet build apps/api/RealEstateStar.Api.sln --no-restore
dotnet test apps/api/RealEstateStar.Api.sln --no-restore
bash apps/api/scripts/coverage.sh --low-only
```

- [ ] **Step 8: Commit**

```bash
git commit -m "feat: create Activities.Pdf — PdfActivity + CmaPdfGenerator extracted from Workers.Leads"
```

---

### Task 9: Create Services.AgentNotifier project (AgentNotificationService)

**Files:**
- Create: `apps/api/RealEstateStar.Services/RealEstateStar.Services.AgentNotifier/RealEstateStar.Services.AgentNotifier.csproj`
- Create: `apps/api/RealEstateStar.Services/RealEstateStar.Services.AgentNotifier/AgentNotificationService.cs`
- Create: `apps/api/RealEstateStar.Services/RealEstateStar.Services.AgentNotifier/AgentNotifierDiagnostics.cs`
- Create: `apps/api/RealEstateStar.Services/RealEstateStar.Services.AgentNotifier/ServiceCollectionExtensions.cs`
- Create: `apps/api/RealEstateStar.Tests/RealEstateStar.Services.Tests/RealEstateStar.Services.AgentNotifier.Tests/`
- Modify: `apps/api/RealEstateStar.Api.sln`

- [ ] **Step 1: Create project file**

Dependencies: `RealEstateStar.Domain` + `RealEstateStar.Workers.Shared`. This is a service (synchronous call), NOT a BackgroundService. The orchestrator calls it directly.

- [ ] **Step 2: Implement `AgentNotificationService` (implements `IAgentNotifier`)**

WhatsApp (primary) + email fallback to the real estate agent. Template-based, no Claude. Receives lead summary, score, CMA result, HomeSearch result. Builds WhatsApp template params or email HTML.

- [ ] **Step 3: Add `AgentNotifierDiagnostics`**

Metrics: `agent_notify.whatsapp_success`, `agent_notify.whatsapp_failed`, `agent_notify.email_fallback`, `agent_notify.draft_duration_ms`.

- [ ] **Step 4: Add `ServiceCollectionExtensions.AddAgentNotifier()`**

- [ ] **Step 5: Create test project + tests**

Test cases: WhatsApp template parameters, email fallback when WhatsApp fails, both-fail logging, correct metric recording.

- [ ] **Step 6: Verify build + run tests + coverage**

- [ ] **Step 7: Commit**

```bash
git commit -m "feat: create Services.AgentNotifier — WhatsApp + email fallback notification to agent"
```

---

### Task 10: Create Services.LeadCommunicator project

**Files:**
- Create: `apps/api/RealEstateStar.Services/RealEstateStar.Services.LeadCommunicator/RealEstateStar.Services.LeadCommunicator.csproj`
- Create: `apps/api/RealEstateStar.Services/RealEstateStar.Services.LeadCommunicator/LeadCommunicationService.cs`
- Create: `apps/api/RealEstateStar.Services/RealEstateStar.Services.LeadCommunicator/LeadEmailDrafter.cs`
- Create: `apps/api/RealEstateStar.Services/RealEstateStar.Services.LeadCommunicator/LeadEmailTemplate.cs`
- Create: `apps/api/RealEstateStar.Services/RealEstateStar.Services.LeadCommunicator/LeadCommunicatorDiagnostics.cs`
- Create: `apps/api/RealEstateStar.Services/RealEstateStar.Services.LeadCommunicator/ServiceCollectionExtensions.cs`
- Create: `apps/api/RealEstateStar.Tests/RealEstateStar.Services.Tests/RealEstateStar.Services.LeadCommunicator.Tests/`
- Modify: `apps/api/RealEstateStar.Api.sln`

- [ ] **Step 1: Create project file**

Dependencies: `RealEstateStar.Domain` + `RealEstateStar.Workers.Shared`.

- [ ] **Step 2: Implement `LeadCommunicationService` (implements `ILeadCommunicationService`)**

Two methods: `DraftAsync` (Claude generates personalized email) and `SendAsync` (Gmail API delivery). Separation means: if send fails, draft is cached and re-send doesn't re-call Claude.

- [ ] **Step 3: Implement `LeadEmailDrafter` (implements `ILeadEmailDrafter`)**

Claude-powered email drafting. Takes lead, score, CMA result, HomeSearch result, agent config. Returns personalized email with full branding (logo, colors, CTA).

- [ ] **Step 4: Implement `LeadEmailTemplate`**

HTML email rendering. Takes the drafted content and wraps it in a branded template with privacy footer, opt-out links, CCPA links. Agent branding from `AgentNotificationConfig`.

- [ ] **Step 5: Add `LeadCommunicatorDiagnostics`**

Metrics: `email.draft_duration_ms`, `email.draft_claude_tokens`, `email.draft_fallback`, `email.send_duration_ms`, `email.send_success`, `email.send_failed`.

- [ ] **Step 6: Add `ServiceCollectionExtensions.AddLeadCommunicator()`**

- [ ] **Step 7: Create test project + tests**

Test cases: email draft with CMA + listings, without CMA, without listings, privacy token signing, HTML encoding, template rendering, send success/failure.

- [ ] **Step 8: Verify build + run tests + coverage**

- [ ] **Step 9: Commit**

```bash
git commit -m "feat: create Services.LeadCommunicator — Claude email drafting + Gmail delivery to leads"
```

---

### Task 11: Create Workers.Lead.Orchestrator project

**Files:**
- Create: `apps/api/RealEstateStar.Workers/RealEstateStar.Workers.Lead.Orchestrator/RealEstateStar.Workers.Lead.Orchestrator.csproj`
- Create: `apps/api/RealEstateStar.Workers/RealEstateStar.Workers.Lead.Orchestrator/LeadOrchestrator.cs`
- Create: `apps/api/RealEstateStar.Workers/RealEstateStar.Workers.Lead.Orchestrator/LeadOrchestratorChannel.cs`
- Create: `apps/api/RealEstateStar.Workers/RealEstateStar.Workers.Lead.Orchestrator/LeadScorer.cs`
- Create: `apps/api/RealEstateStar.Workers/RealEstateStar.Workers.Lead.Orchestrator/ServiceCollectionExtensions.cs`
- Create: `apps/api/RealEstateStar.Tests/RealEstateStar.Workers.Tests/RealEstateStar.Workers.Lead.Orchestrator.Tests/`
- Modify: `apps/api/RealEstateStar.Api.sln`

- [ ] **Step 1: Create project file**

Dependencies: `Domain` + `Workers.Shared` + `Activities.Pdf` + `Services.AgentNotifier` + `Services.LeadCommunicator` + `Workers.Lead.CMA` + `Workers.Lead.HomeSearch`. This is the only project allowed to reference all worker/service/activity projects (composition point for lead pipeline).

- [ ] **Step 2: Implement `LeadOrchestrator`**

BackgroundService that reads from `LeadOrchestratorChannel`. For each lead:
1. Load agent config
2. Build `LeadPipelineContext`
3. Score (sync, pure logic via `ILeadScorer`)
4. Update status to `Scored`
5. Fan-out CMA + HomeSearch via channels with `TaskCompletionSource` for result collection
6. Update status to `Analyzing`
7. If CMA succeeded, call `PdfActivity` directly
8. Call `ILeadCommunicationService.DraftAsync()` then `SendAsync()`
9. Call `IAgentNotifier.NotifyAsync()`
10. Update status to `Notified`
11. Return `LeadPipelineResult`

- [ ] **Step 3: Implement `LeadOrchestratorChannel`**

Channel\<LeadOrchestrationRequest\> with bounded capacity. `LeadOrchestrationRequest` holds `Lead`, `AgentId`, `CorrelationId`.

- [ ] **Step 4: Implement `LeadScorer` (implements `ILeadScorer`)**

Pure logic scoring. Factors: Timeline (0.35), Notes (0.05), Engagement/SubmissionCount (0.10), PropertyDetails (0.25 seller-only or 0.15 if both), PreApproval (0.25 buyer-only or 0.15 if both), BudgetAlignment (0.15 buyer). Bucket: >=70 Hot, >=40 Warm, <40 Cool.

- [ ] **Step 5: Add `ServiceCollectionExtensions.AddLeadOrchestrator()`**

Register `LeadOrchestrator` as hosted service, `LeadOrchestratorChannel` as singleton, `ILeadScorer` as singleton.

- [ ] **Step 6: Create test project + tests**

Test cases:
- Dispatch logic by lead type (seller-only, buyer-only, both)
- Timeout handling (mock slow CMA worker)
- Partial failure (CMA fails, HomeSearch succeeds)
- Score calculation for all lead type combinations
- Score bucket boundaries (69 = Warm, 70 = Hot, 39 = Cool, 40 = Warm)
- Engagement factor for submission counts 1-4+
- Notes factor (with/without notes)

- [ ] **Step 7: Verify build + run tests + coverage**

```bash
dotnet build apps/api/RealEstateStar.Api.sln --no-restore
dotnet test apps/api/RealEstateStar.Api.sln --no-restore
bash apps/api/scripts/coverage.sh --low-only
```

- [ ] **Step 8: Commit**

```bash
git commit -m "feat: create Workers.Lead.Orchestrator — per-lead pipeline with scoring, fan-out, and notifications"
```

---

## Phase 3: Wire New Architecture (depends on Phase 2 — run Tasks 12–13 in parallel)

---

### Task 12: Update Program.cs with self-contained DI registrations

**Files:**
- Modify: `apps/api/RealEstateStar.Api/Program.cs`
- Modify: `apps/api/RealEstateStar.Api/RealEstateStar.Api.csproj`

- [ ] **Step 1: Add project references to Api.csproj**

Add `<ProjectReference>` entries for:
- `RealEstateStar.Workers.Lead.Orchestrator`
- `RealEstateStar.Activities.Pdf`
- `RealEstateStar.Services.AgentNotifier`
- `RealEstateStar.Services.LeadCommunicator`

Remove reference to `RealEstateStar.Workers.Leads` (if still referenced).

- [ ] **Step 2: Replace monolith DI with self-contained extensions**

Replace all inline lead pipeline registrations with:
```csharp
// Lead pipeline — decomposed orchestrator wiring
builder.Services.AddLeadOrchestrator();
builder.Services.AddPdfService();
builder.Services.AddAgentNotifier();
builder.Services.AddLeadCommunicator();
```

Each call delegates to the project's own `ServiceCollectionExtensions`.

- [ ] **Step 3: Register IDiagnosticsProvider implementations**

Add auto-registration that scans for `IDiagnosticsProvider` implementations:
```csharp
builder.Services.AddAllDiagnosticsProviders();
```

- [ ] **Step 4: Verify full build + run all tests**

```bash
dotnet build apps/api/RealEstateStar.Api.sln --no-restore
dotnet test apps/api/RealEstateStar.Api.sln --no-restore
```

- [ ] **Step 5: Commit**

```bash
git commit -m "refactor: wire new decomposed lead pipeline DI in Program.cs"
```

---

### Task 13: Update SubmitLeadEndpoint for new architecture

**Files:**
- Modify: `apps/api/RealEstateStar.Api/Features/Leads/Submit/SubmitLeadEndpoint.cs`
- Modify: `apps/api/RealEstateStar.Tests/RealEstateStar.Api.Tests/Features/Leads/Submit/SubmitLeadEndpointTests.cs`

- [ ] **Step 1: Update endpoint to use `LeadOrchestratorChannel`**

The endpoint should:
1. Validate request
2. Resolve agent config
3. Dedup (GetByEmailAsync — if existing, increment SubmissionCount)
4. Map request notes into SellerDetails/BuyerDetails
5. Save lead
6. Write to `LeadOrchestratorChannel` with `LeadOrchestrationRequest`
7. Return 202 Accepted

- [ ] **Step 2: Update tests**

Verify the endpoint correctly dispatches to the orchestrator channel, increments submission count on dedup, and maps notes.

- [ ] **Step 3: Verify build + run tests**

- [ ] **Step 4: Commit**

```bash
git commit -m "refactor: update SubmitLeadEndpoint to dispatch via LeadOrchestratorChannel"
```

---

## Phase 4: Cleanup (depends on Phase 3 — run Tasks 14–17 in parallel)

---

### Task 14: Delete old Workers.Leads project

**Files:**
- Delete: `apps/api/RealEstateStar.Workers.Leads/` (entire directory)
- Delete: `apps/api/RealEstateStar.Tests/RealEstateStar.Workers.Leads.Tests/` (entire directory, if exists)
- Modify: `apps/api/RealEstateStar.Api.sln` — remove old project entries
- Modify: `apps/api/RealEstateStar.Api/RealEstateStar.Api.csproj` — remove old ProjectReference

- [ ] **Step 1: Verify no remaining references to Workers.Leads**

Search the entire `apps/api/` for references to `Workers.Leads` namespace, `Workers.Leads.csproj`, or any type that was moved. All should already point to new projects.

```bash
grep -r "Workers\.Leads" apps/api/ --include="*.cs" --include="*.csproj"
```

- [ ] **Step 2: Remove from solution file**

```bash
dotnet sln apps/api/RealEstateStar.Api.sln remove apps/api/RealEstateStar.Workers.Leads/RealEstateStar.Workers.Leads.csproj
```

- [ ] **Step 3: Delete directories**

- [ ] **Step 4: Verify build + run all tests**

- [ ] **Step 5: Commit**

```bash
git commit -m "refactor: delete Workers.Leads — all code moved to Orchestrator, Activities.Pdf, Services.*"
```

---

### Task 15: Rename Workers.Cma to Workers.Lead.CMA

**Files:**
- Rename: `apps/api/RealEstateStar.Workers.Cma/` → `apps/api/RealEstateStar.Workers/RealEstateStar.Workers.Lead.CMA/`
- Modify: All `<ProjectReference>` paths referencing the old name
- Modify: `apps/api/RealEstateStar.Api.sln`
- Modify: Namespace in all `.cs` files: `RealEstateStar.Workers.Cma` → `RealEstateStar.Workers.Lead.CMA`
- Rename: test project accordingly

- [ ] **Step 1: Create new directory with renamed project**

- [ ] **Step 2: Update namespaces in all .cs files**

- [ ] **Step 3: Update all ProjectReference paths**

- [ ] **Step 4: Update solution file**

- [ ] **Step 5: Update architecture tests**

- [ ] **Step 6: Verify build + run all tests**

- [ ] **Step 7: Commit**

```bash
git commit -m "refactor: rename Workers.Cma → Workers.Lead.CMA"
```

---

### Task 16: Rename Workers.HomeSearch to Workers.Lead.HomeSearch

**Files:**
- Rename: `apps/api/RealEstateStar.Workers.HomeSearch/` → `apps/api/RealEstateStar.Workers/RealEstateStar.Workers.Lead.HomeSearch/`
- Modify: All `<ProjectReference>` paths
- Modify: `apps/api/RealEstateStar.Api.sln`
- Modify: Namespaces
- Rename: test project

- [ ] **Step 1: Create new directory with renamed project**

- [ ] **Step 2: Update namespaces in all .cs files**

- [ ] **Step 3: Update all ProjectReference paths**

- [ ] **Step 4: Update solution file**

- [ ] **Step 5: Update architecture tests**

- [ ] **Step 6: Verify build + run all tests**

- [ ] **Step 7: Commit**

```bash
git commit -m "refactor: rename Workers.HomeSearch → Workers.Lead.HomeSearch"
```

---

### Task 17: Folder restructure + cleanup

**Files:**
- Move: All `RealEstateStar.Clients.*` projects under `apps/api/RealEstateStar.Clients/`
- Move: All `RealEstateStar.Workers.*` projects under `apps/api/RealEstateStar.Workers/`
- Move: All `RealEstateStar.Services.*` projects under `apps/api/RealEstateStar.Services/`
- Move: All `RealEstateStar.Activities.*` projects under `apps/api/RealEstateStar.Activities/`
- Move: All test projects under `apps/api/RealEstateStar.Tests/`
- Modify: All `<ProjectReference>` paths in `.csproj` files
- Modify: `apps/api/RealEstateStar.Api.sln`
- Remove: Unused usings across all moved files

- [ ] **Step 1: Move Clients projects into Clients/ directory**

Group all 14 `Clients.*` projects. No namespace changes — `RealEstateStar.Clients.Anthropic` stays the same.

- [ ] **Step 2: Move Workers projects into Workers/ directory**

Group `Workers.Shared`, `Workers.Lead.CMA`, `Workers.Lead.HomeSearch`, `Workers.Lead.Orchestrator`, `Workers.WhatsApp`.

- [ ] **Step 3: Move Services projects into Services/ directory**

Group `Services.AgentNotifier`, `Services.LeadCommunicator`.

- [ ] **Step 4: Move Activities projects into Activities/ directory**

Group `Activities.Pdf`.

- [ ] **Step 5: Update ALL ProjectReference paths**

Every `.csproj` that references a moved project needs path updates. Use find-and-replace.

- [ ] **Step 6: Update solution file**

Regenerate or update all project paths in the `.sln` file.

- [ ] **Step 7: Remove unused usings**

Run `dotnet format` or IDE cleanup across all moved files.

- [ ] **Step 8: Verify build + run all tests**

```bash
dotnet build apps/api/RealEstateStar.Api.sln --no-restore
dotnet test apps/api/RealEstateStar.Api.sln --no-restore
```

- [ ] **Step 9: Commit**

```bash
git commit -m "refactor: group projects into Clients/, Workers/, Services/, Activities/ directories"
```

**Note:** This task has a high file-change count. Consider deferring to a separate PR if it creates too much merge conflict risk.

---

## Phase 5: Smart Retry + PersistActivity (depends on Phase 4 — run Tasks 18–22 in parallel)

---

### Task 18: Content hash computation utility

**Files:**
- Create: `apps/api/RealEstateStar.Domain/Shared/ContentHash.cs`
- Test: `apps/api/RealEstateStar.Tests/RealEstateStar.Domain.Tests/Shared/ContentHashTests.cs`

- [ ] **Step 1: Create `ContentHash` static utility**

```csharp
namespace RealEstateStar.Domain.Shared;

public static class ContentHash
{
    public static string Compute(params string?[] fields)
    {
        var input = string.Join("|", fields.Select(f => f ?? ""));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
```

Used by every activity to compute its retry key from input fields.

- [ ] **Step 2: Write tests**

Test cases:
- Same inputs = same hash
- Different inputs = different hash
- Null fields handled gracefully
- Deterministic across calls
- Order matters (different field order = different hash)

- [ ] **Step 3: Verify build + run tests + coverage**

- [ ] **Step 4: Commit**

```bash
git commit -m "feat: add ContentHash utility — SHA256 hashing for activity retry keys"
```

---

### Task 19: Wire LeadRetryState into orchestrator

**Files:**
- Modify: `apps/api/RealEstateStar.Workers/RealEstateStar.Workers.Lead.Orchestrator/LeadOrchestrator.cs`
- Modify: `apps/api/RealEstateStar.Tests/RealEstateStar.Workers.Tests/RealEstateStar.Workers.Lead.Orchestrator.Tests/LeadOrchestratorTests.cs`

- [ ] **Step 1: Add retry key computation per activity**

Before each activity, compute the content hash of its input fields:

```
CMA key:         SHA256(address + city + state + zip)
HomeSearch key:   SHA256(city + state + minBudget + maxBudget + bedrooms + bathrooms)
PDF key:          SHA256(serialized CmaWorkerResult)
DraftEmail key:   SHA256(score hash + cmaResult hash + hsResult hash)
DraftAgent key:   SHA256(score hash + cmaResult hash + hsResult hash)
```

- [ ] **Step 2: Add skip logic**

Before dispatching each activity, check `ctx.RetryState.IsCompleted(activityName, currentHash)`. If true, skip the activity and load the cached result from `CompletedResultPaths`.

- [ ] **Step 3: Update retry state after activity completion**

After each activity completes, store the hash and result path:
```csharp
ctx.RetryState.CompletedActivityKeys[activityName] = currentHash;
ctx.RetryState.CompletedResultPaths[activityName] = resultPath;
```

- [ ] **Step 4: Write tests for retry scenarios**

Test cases:
- Same lead, same property = skip all (CMA, HomeSearch, PDF)
- Same lead, different property = re-run CMA + PDF, skip HomeSearch
- Same lead, different buyer criteria = skip CMA, re-run HomeSearch
- First submission (no retry state) = run everything
- Partial completion (crashed after CMA) = skip CMA, run rest

- [ ] **Step 5: Verify build + run tests + coverage**

- [ ] **Step 6: Commit**

```bash
git commit -m "feat: wire LeadRetryState into orchestrator — content-aware skip/re-run per activity"
```

---

### Task 20: Implement PersistActivity

**Files:**
- Create: `apps/api/RealEstateStar.Workers/RealEstateStar.Workers.Lead.Orchestrator/PersistActivity.cs`
- Test: `apps/api/RealEstateStar.Tests/RealEstateStar.Workers.Tests/RealEstateStar.Workers.Lead.Orchestrator.Tests/PersistActivityTests.cs`

- [ ] **Step 1: Implement `PersistActivity`**

Reads the entire `LeadPipelineContext` and writes all artifacts in a single batch:

```
Lead folder: Real Estate Star/1 - Leads/{FullName}/
  +-- Lead Profile.md              ← upsert: status, score, bucket, submission_count
  +-- CMA Summary.md               ← upsert: estimated value, comps, market analysis
  +-- HomeSearch Summary.md         ← upsert: listings, area summary
  +-- Lead Email Draft.md           ← upsert: subject + body + sent status + channel
  +-- Agent Notification Draft.md   ← upsert: subject + body + sent status + channel
  +-- Retry State.json              ← upsert: content hashes per activity
```

PDF is NOT written here (already written by PdfActivity — too large for context).

- [ ] **Step 2: Implement communication dedup**

For each communication document: check if existing file has same `content_hash` in YAML frontmatter AND `sent: true`. If so, skip the write — prevents duplicate drafts on retry.

- [ ] **Step 3: Write tests**

Test cases:
- Full pipeline result persisted correctly
- Partial result (CMA failed) — only writes available summaries
- Communication dedup — same hash + sent = skip
- Communication dedup — same hash + not sent = overwrite
- Communication dedup — different hash = overwrite
- Retry state serialized correctly as JSON
- Idempotent — calling twice with same input produces same output

- [ ] **Step 4: Wire PersistActivity into orchestrator as final step**

- [ ] **Step 5: Verify build + run tests + coverage**

- [ ] **Step 6: Commit**

```bash
git commit -m "feat: add PersistActivity — batch upsert of all pipeline artifacts with content-hash dedup"
```

---

### Task 21: Cross-lead content cache (spam protection)

**Files:**
- Create: `apps/api/RealEstateStar.Workers/RealEstateStar.Workers.Lead.Orchestrator/ContentCache.cs`
- Test: `apps/api/RealEstateStar.Tests/RealEstateStar.Workers.Tests/RealEstateStar.Workers.Lead.Orchestrator.Tests/ContentCacheTests.cs`

- [ ] **Step 1: Implement `IContentCache` interface and `MemoryContentCache`**

Per-lead retry state protects a single lead from re-running unchanged steps. The content cache protects against 100 different people submitting for the same property.

```
CMA request for "123 Main St, Newark, NJ 07102"
  → SHA256("123 Main St|Newark|NJ|07102") = "abc123"
  → Check shared cache: has "abc123" been computed in last 24 hours?
    YES → Return cached CmaWorkerResult
    NO  → Execute CMA activity, cache result with 24hr TTL
```

Cache TTLs:
- CMA: 24 hours (comps are stable)
- HomeSearch: 1 hour (listings change frequently)
- PDF: 24 hours (tied to CMA result)
- Notifications: no cache (always send)

- [ ] **Step 2: Wire cache into orchestrator**

Before each cacheable activity, check the shared content cache. On cache hit, skip execution and use cached result. On cache miss, execute and store result.

- [ ] **Step 3: Add cache diagnostics**

Metrics: `cache.hit`, `cache.miss`, `cache.evicted` (per activity type).

- [ ] **Step 4: Write tests**

Test cases:
- Cache miss → execute → cache stored
- Cache hit → skip execution → return cached result
- Different inputs → cache miss (different hash)
- TTL expiry → cache miss after expiry
- Concurrent access safety

- [ ] **Step 5: Verify build + run tests + coverage**

- [ ] **Step 6: Commit**

```bash
git commit -m "feat: add cross-lead content cache — shared IMemoryCache dedup for spam protection"
```

---

### Task 22: Update SubmitLeadEndpoint for status-based gating

**Files:**
- Modify: `apps/api/RealEstateStar.Api/Features/Leads/Submit/SubmitLeadEndpoint.cs`
- Modify: `apps/api/RealEstateStar.Tests/RealEstateStar.Api.Tests/Features/Leads/Submit/SubmitLeadEndpointTests.cs`

- [ ] **Step 1: Add status check before dispatch**

When a dedup'd lead is found, check its current status:

```
Received?           → Start orchestrator (first time or crashed before scoring)
Scored/Analyzing?   → Already in progress — skip dispatch, return 200 "processing"
Complete?           → Check content hash — re-run if input changed, skip if same
Notified?           → Already in progress — skip dispatch
```

Without this, 100 rapid submissions would spawn 100 orchestrator instances all racing through the pipeline.

- [ ] **Step 2: Write tests for each status gate**

Test cases:
- New lead (no existing) → dispatch, 202
- Existing lead, status Received → dispatch, 202
- Existing lead, status Scored → skip, 200 "processing"
- Existing lead, status Analyzing → skip, 200 "processing"
- Existing lead, status Complete, same content → skip, 200 "already complete"
- Existing lead, status Complete, different content → dispatch, 202

- [ ] **Step 3: Verify build + run tests + coverage**

- [ ] **Step 4: Commit**

```bash
git commit -m "feat: add status-based gating to SubmitLeadEndpoint — prevent duplicate orchestrator instances"
```

---

## Phase 6: Exhaustive Dependency Tests (depends on Phase 5 — run Tasks 23–25 in parallel)

---

### Task 23: Per-project allowed-dependency test

**Files:**
- Modify: `apps/api/RealEstateStar.Tests/RealEstateStar.Architecture.Tests/DependencyTests.cs`

- [ ] **Step 1: Add exhaustive allowed-dependencies Theory**

One test, one source-of-truth table. Every project lists its allowed dependencies. Any dependency not in the list = test failure.

```csharp
[Theory]
[InlineData("RealEstateStar.Domain", new string[] { })]
[InlineData("RealEstateStar.Data", new[] { "Domain" })]
[InlineData("RealEstateStar.DataServices", new[] { "Domain" })]
[InlineData("RealEstateStar.Notifications", new[] { "Domain" })]
[InlineData("RealEstateStar.Workers.Shared", new[] { "Domain" })]
[InlineData("RealEstateStar.Activities.Pdf", new[] { "Domain", "Workers.Shared" })]
[InlineData("RealEstateStar.Services.AgentNotifier", new[] { "Domain", "Workers.Shared" })]
[InlineData("RealEstateStar.Services.LeadCommunicator", new[] { "Domain", "Workers.Shared" })]
[InlineData("RealEstateStar.Workers.Lead.CMA", new[] { "Domain", "Workers.Shared" })]
[InlineData("RealEstateStar.Workers.Lead.HomeSearch", new[] { "Domain", "Workers.Shared" })]
[InlineData("RealEstateStar.Workers.WhatsApp", new[] { "Domain", "Workers.Shared" })]
[InlineData("RealEstateStar.Workers.Lead.Orchestrator", new[] {
    "Domain", "Workers.Shared", "Activities.Pdf",
    "Services.AgentNotifier", "Services.LeadCommunicator",
    "Workers.Lead.CMA", "Workers.Lead.HomeSearch" })]
[InlineData("RealEstateStar.Clients.Anthropic", new[] { "Domain" })]
[InlineData("RealEstateStar.Clients.Azure", new[] { "Domain" })]
[InlineData("RealEstateStar.Clients.Cloudflare", new[] { "Domain" })]
[InlineData("RealEstateStar.Clients.GDrive", new[] { "Domain", "Clients.GoogleOAuth" })]
[InlineData("RealEstateStar.Clients.Gmail", new[] { "Domain", "Clients.GoogleOAuth" })]
[InlineData("RealEstateStar.Clients.GDocs", new[] { "Domain", "Clients.GoogleOAuth" })]
[InlineData("RealEstateStar.Clients.GSheets", new[] { "Domain", "Clients.GoogleOAuth" })]
[InlineData("RealEstateStar.Clients.GoogleOAuth", new[] { "Domain" })]
[InlineData("RealEstateStar.Clients.Gws", new[] { "Domain" })]
[InlineData("RealEstateStar.Clients.RentCast", new[] { "Domain" })]
[InlineData("RealEstateStar.Clients.Scraper", new[] { "Domain" })]
[InlineData("RealEstateStar.Clients.Stripe", new[] { "Domain" })]
[InlineData("RealEstateStar.Clients.Turnstile", new[] { "Domain" })]
[InlineData("RealEstateStar.Clients.WhatsApp", new[] { "Domain" })]
```

Key constraints:
- `Activities.Pdf` must NOT reference `Workers.Lead.*`
- `Services.AgentNotifier` must NOT reference `Workers.Lead.*`
- `Services.LeadCommunicator` must NOT reference `Workers.Lead.*`
- Only `Workers.Lead.Orchestrator` may reference all projects

- [ ] **Step 2: Add symmetric tests to LayerTests.cs (NetArchTest)**

Add equivalent constraints in the NetArchTest-based test suite. Both suites must be maintained (belt-and-suspenders per project convention).

- [ ] **Step 3: Verify all architecture tests pass**

```bash
dotnet test apps/api/RealEstateStar.Tests/RealEstateStar.Architecture.Tests --no-restore
```

- [ ] **Step 4: Commit**

```bash
git commit -m "test: add exhaustive per-project allowed-dependency tests for restructured architecture"
```

---

### Task 24: csproj reference audit test

**Files:**
- Create or modify: `apps/api/RealEstateStar.Tests/RealEstateStar.Architecture.Tests/CsprojAuditTests.cs`

- [ ] **Step 1: Implement csproj reference audit test**

The assembly-level test catches runtime dependencies. This test catches compile-time references in `.csproj` files — a project might reference another project's csproj but never actually use any types (latent violation).

```csharp
[Fact]
public void AllCsprojReferences_MatchAllowedDependencyTable()
{
    // Parse every .csproj under apps/api/
    // Extract <ProjectReference> elements
    // Verify each reference is in the allowed table
    // Fail with: "MyProject.csproj references DisallowedProject.csproj"
}
```

- [ ] **Step 2: Write tests that scan all .csproj files**

Use `Directory.GetFiles("*.csproj", SearchOption.AllDirectories)` to find all project files. Parse XML to extract `<ProjectReference>` elements. Validate against the same allowed-dependency table.

- [ ] **Step 3: Verify test passes**

- [ ] **Step 4: Commit**

```bash
git commit -m "test: add csproj reference audit — catches compile-time dependency violations"
```

---

### Task 25: Single-purpose project manifest test

**Files:**
- Create or modify: `apps/api/RealEstateStar.Tests/RealEstateStar.Architecture.Tests/ProjectManifestTests.cs`

- [ ] **Step 1: Implement single-purpose validation**

Each project must have a `<Description>` element in its `.csproj` matching a known purpose. Forces every new project to declare its intent at creation time.

```csharp
[Theory]
[InlineData("RealEstateStar.Domain", "Pure models, interfaces, enums — zero dependencies")]
[InlineData("RealEstateStar.Activities.Pdf", "PDF generation activity — QuestPDF CMA reports")]
[InlineData("RealEstateStar.Services.AgentNotifier", "Agent notification service — WhatsApp + email fallback")]
[InlineData("RealEstateStar.Services.LeadCommunicator", "Lead communication service — Claude email drafting + Gmail delivery")]
[InlineData("RealEstateStar.Workers.Lead.Orchestrator", "Lead pipeline orchestration — scoring, dispatch, coordination")]
[InlineData("RealEstateStar.Workers.Lead.CMA", "CMA pipeline worker")]
[InlineData("RealEstateStar.Workers.Lead.HomeSearch", "Home search pipeline worker")]
// ... every project
public void Project_HasSinglePurpose_DocumentedInManifest(string project, string purpose)
{
    // Verify the project's .csproj has a <Description> element matching the purpose
}
```

- [ ] **Step 2: Add `<Description>` elements to all .csproj files**

Every project file must declare its purpose.

- [ ] **Step 3: Verify test passes**

- [ ] **Step 4: Commit**

```bash
git commit -m "test: add single-purpose project manifest — every project declares its intent in csproj"
```

---

## Post-Implementation (run Tasks 26–28 in parallel)

---

### Task 26: Update CLAUDE.md with new structure

**Files:**
- Modify: `CLAUDE.md` (root)
- Modify: `.claude/CLAUDE.md`

- [ ] **Step 1: Update monorepo structure diagram**

Add `Activities/`, `Services/` groupings. Update `Workers/` to show `Workers.Lead.CMA`, `Workers.Lead.HomeSearch`, `Workers.Lead.Orchestrator`.

- [ ] **Step 2: Update API dependency rules**

Add the new projects and their dependency rules:
```
Activities.Pdf            → Domain + Workers.Shared
Services.AgentNotifier    → Domain + Workers.Shared
Services.LeadCommunicator → Domain + Workers.Shared
Workers.Lead.Orchestrator → Domain + Workers.Shared + Activities.Pdf
                          + Services.AgentNotifier + Services.LeadCommunicator
                          + Workers.Lead.CMA + Workers.Lead.HomeSearch
```

- [ ] **Step 3: Update infrastructure table**

Add entries for new projects explaining their purpose.

- [ ] **Step 4: Commit**

```bash
git commit -m "docs: update CLAUDE.md with restructured worker architecture"
```

---

### Task 27: Update architecture diagrams

**Files:**
- Modify: `docs/architecture/README.md`

- [ ] **Step 1: Update dependency diagram**

Show the new project layout with `Activities.*`, `Services.*`, and `Workers.Lead.*` groupings.

- [ ] **Step 2: Update lead pipeline flow diagram**

Show the per-lead orchestrator pattern: Score → Fan-out → PDF → Communication → Notification → Persist.

- [ ] **Step 3: Add smart retry diagram**

Show the content hash decision flow: compute hash → check cache → skip or execute → update cache.

- [ ] **Step 4: Commit**

```bash
git commit -m "docs: update architecture diagrams for worker restructure"
```

---

### Task 28: Run code review + security review + observability audit

**Files:**
- No file changes — review only

- [ ] **Step 1: Run code-reviewer agent**

Review all new code for quality, patterns, and consistency.

- [ ] **Step 2: Run security-reviewer agent**

Check for:
- No PII in span tags or log fields
- Content hash inputs don't leak to logs
- YAML injection prevention in PersistActivity
- Path traversal protection in file write operations

- [ ] **Step 3: Verify observability parity**

Ensure all existing diagnostics modules, counters, histograms, tracing sources, health checks, and log codes are preserved or improved. Check:
- OrchestratorDiagnostics metrics maintained
- LeadDiagnostics merged into orchestrator diagnostics
- CmaDiagnostics in Workers.Lead.CMA
- HomeSearchDiagnostics in Workers.Lead.HomeSearch
- PdfDiagnostics in Activities.Pdf
- New activity spans: `activity.score`, `activity.cma`, `activity.home_search`, `activity.pdf`, `activity.draft_lead_email`, `activity.send_lead_email`, `activity.draft_agent_notification`, `activity.send_agent_notification`, `activity.persist`
- New log codes: `ORCH-0xx`, `DRAFT-0xx`, `SEND-0xx`, `CACHE-0xx`, `PDF-0xx`, `PERSIST-0xx`

- [ ] **Step 4: Run full test suite + coverage report**

```bash
dotnet test apps/api/RealEstateStar.Api.sln --no-restore
bash apps/api/scripts/coverage.sh --low-only
```

Verify 100% branch coverage on all new code.

- [ ] **Step 5: Document any findings as GitHub Issues**
