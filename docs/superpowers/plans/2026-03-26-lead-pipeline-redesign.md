# Lead Pipeline Redesign — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the person-scraping lead pipeline with an orchestrator that coordinates CMA/HomeSearch workers, then sends a branded email to the lead and a WhatsApp summary to the agent.

**Architecture:** New `LeadOrchestrator` (standalone BackgroundService) dispatches CMA/HomeSearch workers via Channel + TaskCompletionSource, collects results, dispatches PDF generation, then triggers Gmail email to lead and WhatsApp (with email fallback) to agent. Workers become storage-I/O-free. Pure-logic lead scoring replaces Claude-based enrichment.

**Tech Stack:** .NET 10, Channel<T>, TaskCompletionSource, QuestPDF, Gmail API, WhatsApp Business API, Polly retry, OpenTelemetry

**Spec:** `docs/superpowers/specs/2026-03-26-lead-pipeline-redesign-design.md`

---

## Spawn Pattern

```
Phase 1: Launch Tasks 1–5 in parallel (5 agents) — Domain models + interfaces (including IAgentNotifier, ILeadEmailDrafter)
Phase 1.5: Launch Task 6 alone (1 agent) — LeadStatus + cleanup (broad blast radius, must run alone)
Phase 2: Launch Tasks 7–10 in parallel (4 agents) — Workers refactored + PDF worker + scoring
Phase 3: Launch Tasks 11–13 in parallel (3 agents) — Orchestrator + email template + WhatsApp
Phase 4: Launch Tasks 14–16 in parallel (3 agents) — DI wiring + cleanup + architecture tests
Phase 5: Launch Task 17 (1 agent) — Diagnostics + Grafana dashboard
Phase 6: Launch Task 18 (1 agent) — End-to-end integration test
```

**Important codebase notes for implementers:**
- WhatsApp interface is `IWhatsAppSender` (not IWhatsAppSender). Located at `Domain/Shared/Interfaces/Senders/IWhatsAppSender.cs`.
- `CompSummary`/`ListingSummary` (Task 2) must align with existing CMA `Comp` and HomeSearch listing models — read those files before implementing.
- `DiRegistrationTests.cs` and `TestWebApplicationFactory` both reference removed interfaces — must be updated in Task 14.
- The `CascadingAgentNotifier` in Notifications/ is replaced by the new `AgentNotifier` — listed for deletion in Task 15.

---

## File Structure

**Files to create:**
- `apps/api/RealEstateStar.Domain/Leads/Interfaces/ILeadScorer.cs` — new scoring interface
- `apps/api/RealEstateStar.Domain/Leads/Interfaces/ILeadEmailDrafter.cs` — email drafting interface (Phase 1)
- `apps/api/RealEstateStar.Domain/Leads/Interfaces/IAgentNotifier.cs` — agent notification interface (Phase 1)
- `apps/api/RealEstateStar.Domain/Leads/Models/AgentNotificationConfig.cs` — agent config subset for dispatch payloads
- `apps/api/RealEstateStar.Domain/Leads/Models/WorkerResults.cs` — sealed result hierarchy
- `apps/api/RealEstateStar.Domain/Orchestration/OrchestratorDiagnostics.cs` — new diagnostics class
- `apps/api/RealEstateStar.Workers.Leads/LeadScorer.cs` — pure-logic scoring implementation
- `apps/api/RealEstateStar.Workers.Leads/LeadOrchestrator.cs` — new pipeline coordinator
- `apps/api/RealEstateStar.Workers.Leads/LeadOrchestratorChannel.cs` — channel for orchestrator
- `apps/api/RealEstateStar.Workers.Leads/LeadEmailDrafter.cs` — Claude email drafting
- `apps/api/RealEstateStar.Workers.Leads/LeadEmailTemplate.cs` — HTML email rendering
- `apps/api/RealEstateStar.Workers.Leads/AgentNotifier.cs` — WhatsApp + email fallback
- `apps/api/RealEstateStar.Workers.Leads/PdfWorker.cs` — PDF generation worker
- `apps/api/RealEstateStar.Workers.Leads/PdfProcessingChannel.cs` — channel for PDF worker
- `apps/api/tests/RealEstateStar.Workers.Leads.Tests/LeadScorerTests.cs`
- `apps/api/tests/RealEstateStar.Workers.Leads.Tests/LeadOrchestratorTests.cs`
- `apps/api/tests/RealEstateStar.Workers.Leads.Tests/LeadEmailDrafterTests.cs`
- `apps/api/tests/RealEstateStar.Workers.Leads.Tests/LeadEmailTemplateTests.cs`
- `apps/api/tests/RealEstateStar.Workers.Leads.Tests/AgentNotifierTests.cs`
- `apps/api/tests/RealEstateStar.Workers.Leads.Tests/PdfWorkerTests.cs`

**Files to modify:**
- `apps/api/RealEstateStar.Domain/Leads/Models/LeadStatus.cs` — new enum values, remove old
- `apps/api/RealEstateStar.Domain/Leads/Models/Lead.cs` — remove `Enrichment` property
- `apps/api/RealEstateStar.Workers.Cma/CmaProcessingWorker.cs` — remove storage/notification, add TaskCompletionSource
- `apps/api/RealEstateStar.Workers.Cma/CmaPipelineContext.cs` — add completion source
- `apps/api/RealEstateStar.Workers.Cma/CmaProcessingChannel.cs` — add TCS to request
- `apps/api/RealEstateStar.Workers.HomeSearch/HomeSearchProcessingWorker.cs` — same refactor
- `apps/api/RealEstateStar.Workers.HomeSearch/HomeSearchPipelineContext.cs` — add completion source
- `apps/api/RealEstateStar.Workers.HomeSearch/HomeSearchProcessingChannel.cs` — add TCS to request
- `apps/api/RealEstateStar.Api/Program.cs` — DI rewiring
- `apps/api/RealEstateStar.Api/Diagnostics/OpenTelemetryExtensions.cs` — register orchestrator metrics
- `apps/api/RealEstateStar.Api/appsettings.json` — add `Pipeline:Lead:WorkerTimeoutSeconds`
- `apps/api/tests/RealEstateStar.Architecture.Tests/DependencyTests.cs` — update dependency rules
- `infra/grafana/real-estate-star-api-dashboard.json` — add orchestrator row

**Files to delete:**
- `apps/api/RealEstateStar.Workers.Leads/ScraperLeadEnricher.cs`
- `apps/api/RealEstateStar.Workers.Leads/LeadProcessingWorker.cs`
- `apps/api/RealEstateStar.Workers.Leads/LeadPipelineContext.cs`
- `apps/api/RealEstateStar.Workers.Leads/LeadProcessingChannel.cs`
- `apps/api/RealEstateStar.Domain/Leads/Models/LeadEnrichment.cs`
- `apps/api/RealEstateStar.Domain/Leads/Interfaces/ILeadEnricher.cs`
- `apps/api/RealEstateStar.Domain/Leads/Interfaces/ILeadNotifier.cs`
- `apps/api/RealEstateStar.Domain/Leads/Interfaces/IFailedNotificationStore.cs`
- `apps/api/RealEstateStar.Notifications/Leads/MultiChannelLeadNotifier.cs`
- `apps/api/RealEstateStar.Notifications/Leads/CascadingAgentNotifier.cs`
- `apps/api/RealEstateStar.Notifications/Cma/CmaSellerNotifier.cs`
- `apps/api/RealEstateStar.Notifications/HomeSearch/HomeSearchBuyerNotifier.cs`
- `apps/api/tests/RealEstateStar.Workers.Leads.Tests/ScraperLeadEnricherTests.cs`
- `apps/api/tests/RealEstateStar.Notifications.Tests/Leads/CascadingAgentNotifierTests.cs` (if exists)
- `apps/api/tests/RealEstateStar.Notifications.Tests/Cma/CmaSellerNotifierTests.cs` (if exists)
- `apps/api/tests/RealEstateStar.Notifications.Tests/HomeSearch/HomeSearchBuyerNotifierTests.cs` (if exists)

---

## Phase 1: Domain Foundation (all independent — run Tasks 1–4 in parallel)

---

### Task 1: ILeadScorer interface + LeadScore pure-logic model updates

**Files:**
- Create: `apps/api/RealEstateStar.Domain/Leads/Interfaces/ILeadScorer.cs`
- Modify: `apps/api/RealEstateStar.Domain/Leads/Models/LeadScore.cs`
- Test: `apps/api/tests/RealEstateStar.Domain.Tests/Leads/LeadScoreTests.cs` (if exists, else create)

- [ ] **Step 1: Create `ILeadScorer` interface**

```csharp
// apps/api/RealEstateStar.Domain/Leads/Interfaces/ILeadScorer.cs
namespace RealEstateStar.Domain.Leads.Interfaces;

public interface ILeadScorer
{
    LeadScore Score(Lead lead);
}
```

Note: synchronous — no external calls, pure logic.

- [ ] **Step 2: Add `Bucket` property to `LeadScore`**

Add to `LeadScore.cs`:

```csharp
public string Bucket => OverallScore switch
{
    >= 70 => "Hot",
    >= 40 => "Warm",
    _ => "Cool"
};
```

- [ ] **Step 3: Verify build**

```bash
dotnet build apps/api/RealEstateStar.Domain/RealEstateStar.Domain.csproj --no-restore
```

- [ ] **Step 4: Commit**

```bash
git add apps/api/RealEstateStar.Domain/Leads/Interfaces/ILeadScorer.cs apps/api/RealEstateStar.Domain/Leads/Models/LeadScore.cs
git commit -m "feat: add ILeadScorer interface + Bucket property on LeadScore"
```

---

### Task 2: Worker result sealed hierarchy

**Files:**
- Create: `apps/api/RealEstateStar.Domain/Leads/Models/WorkerResults.cs`

- [ ] **Step 1: Create the sealed result types**

```csharp
// apps/api/RealEstateStar.Domain/Leads/Models/WorkerResults.cs
namespace RealEstateStar.Domain.Leads.Models;

public abstract record WorkerResult(string LeadId, bool Success, string? Error);

public sealed record CmaWorkerResult(
    string LeadId, bool Success, string? Error,
    decimal? EstimatedValue, decimal? PriceRangeLow, decimal? PriceRangeHigh,
    IReadOnlyList<CompSummary>? Comps, string? MarketAnalysis
) : WorkerResult(LeadId, Success, Error);

public sealed record HomeSearchWorkerResult(
    string LeadId, bool Success, string? Error,
    IReadOnlyList<ListingSummary>? Listings, string? AreaSummary
) : WorkerResult(LeadId, Success, Error);

public sealed record PdfWorkerResult(
    string LeadId, bool Success, string? Error,
    string? StoragePath
) : WorkerResult(LeadId, Success, Error);

public record CompSummary(
    string Address, decimal Price, int? Beds, decimal? Baths,
    int? Sqft, int? DaysOnMarket, double? Distance);

public record ListingSummary(
    string Address, decimal Price, int? Beds, decimal? Baths,
    int? Sqft, string? Status, string? Url);
```

- [ ] **Step 2: Verify build**

```bash
dotnet build apps/api/RealEstateStar.Domain/RealEstateStar.Domain.csproj --no-restore
```

- [ ] **Step 3: Commit**

```bash
git add apps/api/RealEstateStar.Domain/Leads/Models/WorkerResults.cs
git commit -m "feat: add sealed WorkerResult hierarchy — CMA, HomeSearch, PDF result types"
```

---

### Task 3: AgentNotificationConfig model

**Files:**
- Create: `apps/api/RealEstateStar.Domain/Leads/Models/AgentNotificationConfig.cs`

- [ ] **Step 1: Create the config subset model**

```csharp
// apps/api/RealEstateStar.Domain/Leads/Models/AgentNotificationConfig.cs
namespace RealEstateStar.Domain.Leads.Models;

/// <summary>
/// Subset of agent account.json + content.json passed in dispatch payloads.
/// Sized to stay under 64KB for future Azure Queue compatibility.
/// </summary>
public record AgentNotificationConfig
{
    public required string AgentId { get; init; }
    public required string Handle { get; init; }
    public required string Name { get; init; }
    public required string FirstName { get; init; }
    public required string Email { get; init; }
    public required string Phone { get; init; }
    public required string LicenseNumber { get; init; }
    public required string BrokerageName { get; init; }
    public string? BrokerageLogo { get; init; }
    public required string PrimaryColor { get; init; }
    public required string AccentColor { get; init; }
    public required string State { get; init; }
    public IReadOnlyList<string> ServiceAreas { get; init; } = [];
    public string? Bio { get; init; }
    public IReadOnlyList<string> Specialties { get; init; } = [];
    public IReadOnlyList<string> Testimonials { get; init; } = [];
    public string? WhatsAppPhoneNumberId { get; init; }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build apps/api/RealEstateStar.Domain/RealEstateStar.Domain.csproj --no-restore
```

- [ ] **Step 3: Commit**

```bash
git add apps/api/RealEstateStar.Domain/Leads/Models/AgentNotificationConfig.cs
git commit -m "feat: add AgentNotificationConfig — payload subset for worker dispatch"
```

---

### Task 4: Update LeadStatus enum + remove LeadEnrichment

**Files:**
- Modify: `apps/api/RealEstateStar.Domain/Leads/Models/LeadStatus.cs`
- Modify: `apps/api/RealEstateStar.Domain/Leads/Models/Lead.cs`
- Delete: `apps/api/RealEstateStar.Domain/Leads/Models/LeadEnrichment.cs`
- Delete: `apps/api/RealEstateStar.Domain/Leads/Interfaces/ILeadEnricher.cs`
- Delete: `apps/api/RealEstateStar.Domain/Leads/Interfaces/ILeadNotifier.cs`
- Delete: `apps/api/RealEstateStar.Domain/Leads/Interfaces/IFailedNotificationStore.cs`

- [ ] **Step 1: Update `LeadStatus` enum**

Replace the current enum values with:

```csharp
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LeadStatus
{
    Received,
    Scored,
    Analyzing,
    Notified,
    Complete,
    ActiveClient,
    UnderContract,
    Closed,
    Inactive
}
```

Remove: `Enriching`, `Enriched`, `EnrichmentFailed`, `EmailDrafted`, `NotificationFailed`, `CmaComplete`, `SearchComplete`.

- [ ] **Step 2: Remove `Enrichment` property from `Lead.cs`**

Remove `public LeadEnrichment? Enrichment { get; set; }` from `Lead.cs`. Keep `LeadScore? Score`.

- [ ] **Step 3: Delete obsolete files**

Delete these files:
- `apps/api/RealEstateStar.Domain/Leads/Models/LeadEnrichment.cs`
- `apps/api/RealEstateStar.Domain/Leads/Interfaces/ILeadEnricher.cs`
- `apps/api/RealEstateStar.Domain/Leads/Interfaces/ILeadNotifier.cs`
- `apps/api/RealEstateStar.Domain/Leads/Interfaces/IFailedNotificationStore.cs`

- [ ] **Step 4: Fix compilation errors**

Search the entire `apps/api/` for references to `LeadEnrichment`, `ILeadEnricher`, `ILeadNotifier`, `IFailedNotificationStore`, and the removed `LeadStatus` values. Comment out or stub any references — they'll be replaced in later tasks. The goal is a green build.

```bash
dotnet build apps/api/RealEstateStar.Api.sln --no-restore
```

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor: update LeadStatus enum, remove LeadEnrichment + enricher/notifier interfaces"
```

---

## Phase 2: Workers + Scoring (run Tasks 5–8 in parallel, after Phase 1)

---

### Task 5: LeadScorer implementation

**Files:**
- Create: `apps/api/RealEstateStar.Workers.Leads/LeadScorer.cs`
- Create: `apps/api/tests/RealEstateStar.Workers.Leads.Tests/LeadScorerTests.cs`

- [ ] **Step 1: Write failing tests**

Test cases:
1. Seller with ASAP timeline, full property details → score ~85-100 (hot)
2. Buyer with "Just Curious" timeline, no pre-approval, no budget → score ~15-20 (cool)
3. Both buyer+seller with 1-3mo timeline, pre-approved, full details → score ~80+ (hot)
4. Seller with address only, 6-12mo timeline → score ~35-40 (cool/warm boundary)
5. Buyer with pre-approved, ASAP, full budget → score ~90+ (hot)
6. Notes provided adds 5 points to weighted score
7. Bucket property: >=70 "Hot", >=40 "Warm", <40 "Cool"
8. Explanation string contains timeline and lead type context
9. Factors list contains one entry per applicable factor with correct weights

```bash
dotnet test apps/api/tests/RealEstateStar.Workers.Leads.Tests --filter "FullyQualifiedName~LeadScorerTests" --no-restore
```

Expected: FAIL (class not implemented)

- [ ] **Step 2: Implement `LeadScorer`**

```csharp
// apps/api/RealEstateStar.Workers.Leads/LeadScorer.cs
namespace RealEstateStar.Workers.Leads;

public class LeadScorer : ILeadScorer
{
    public LeadScore Score(Lead lead)
    {
        var factors = new List<ScoreFactor>();
        var isSeller = lead.SellerDetails is not null;
        var isBuyer = lead.BuyerDetails is not null;

        // Timeline (always 0.35)
        var timelineScore = lead.Timeline switch
        {
            "asap" => 100,
            "1-3months" => 80,
            "3-6months" => 50,
            "6-12months" => 25,
            "justcurious" => 10,
            _ => 10
        };
        factors.Add(new ScoreFactor
        {
            Category = "Timeline", Score = timelineScore,
            Weight = 0.35m, Explanation = $"Timeline: {lead.Timeline}"
        });

        // Notes (always 0.05)
        var notesScore = string.IsNullOrWhiteSpace(lead.Notes) ? 0 : 100;
        factors.Add(new ScoreFactor
        {
            Category = "Notes", Score = notesScore,
            Weight = 0.05m, Explanation = notesScore > 0 ? "Additional notes provided" : "No notes"
        });

        if (isSeller)
        {
            var s = lead.SellerDetails!;
            var detailScore = (s.Bedrooms.HasValue && s.Bathrooms.HasValue && s.SquareFootage.HasValue) ? 100
                : (s.Bedrooms.HasValue || s.Bathrooms.HasValue) ? 60 : 40;
            factors.Add(new ScoreFactor
            {
                Category = "PropertyDetails", Score = detailScore,
                Weight = isBuyer ? 0.15m : 0.25m,
                Explanation = $"Property detail completeness: {detailScore}%"
            });
        }

        if (isBuyer)
        {
            var b = lead.BuyerDetails!;
            var preApprovalScore = b.PreApproved switch
            {
                "yes" => 100, "in-progress" => 60, _ => 20
            };
            factors.Add(new ScoreFactor
            {
                Category = "PreApproval", Score = preApprovalScore,
                Weight = isSeller ? 0.15m : 0.25m,
                Explanation = $"Pre-approval: {b.PreApproved ?? "none"}"
            });

            var budgetScore = (b.MinBudget.HasValue && b.MaxBudget.HasValue) ? 100
                : (b.MinBudget.HasValue || b.MaxBudget.HasValue) ? 60 : 20;
            factors.Add(new ScoreFactor
            {
                Category = "BudgetAlignment", Score = budgetScore,
                Weight = 0.15m,
                Explanation = $"Budget specified: {budgetScore}%"
            });
        }

        // Normalize: remaining weight if only seller or only buyer
        var totalWeight = factors.Sum(f => f.Weight);
        var overall = (int)Math.Round(factors.Sum(f => f.Score * f.Weight) / totalWeight);
        var bucket = overall >= 70 ? "Hot" : overall >= 40 ? "Warm" : "Cool";
        var type = (isSeller, isBuyer) switch
        {
            (true, true) => "buyer/seller",
            (true, false) => "seller",
            _ => "buyer"
        };

        return new LeadScore
        {
            OverallScore = overall,
            Factors = factors,
            Explanation = $"{bucket} {type} lead — {lead.Timeline} timeline, score {overall}/100"
        };
    }
}
```

- [ ] **Step 3: Run tests**

```bash
dotnet test apps/api/tests/RealEstateStar.Workers.Leads.Tests --filter "FullyQualifiedName~LeadScorerTests" --no-restore
```

Expected: all pass.

- [ ] **Step 4: Run coverage**

```bash
bash apps/api/scripts/coverage.sh --low-only
```

Verify `LeadScorer` has 100% branch coverage.

- [ ] **Step 5: Commit**

```bash
git add apps/api/RealEstateStar.Workers.Leads/LeadScorer.cs apps/api/tests/RealEstateStar.Workers.Leads.Tests/LeadScorerTests.cs
git commit -m "feat: add LeadScorer — pure logic scoring from form data"
```

---

### Task 6: Refactor CMA worker — remove storage writes, add TaskCompletionSource

**Files:**
- Modify: `apps/api/RealEstateStar.Workers.Cma/CmaProcessingWorker.cs`
- Modify: `apps/api/RealEstateStar.Workers.Cma/CmaProcessingChannel.cs`
- Modify: `apps/api/RealEstateStar.Workers.Cma/CmaPipelineContext.cs`
- Modify: `apps/api/tests/RealEstateStar.Workers.Cma.Tests/` — update tests

- [ ] **Step 1: Add `TaskCompletionSource<CmaWorkerResult>` to `CmaProcessingRequest`**

In `CmaProcessingChannel.cs`, update the request record:

```csharp
public record CmaProcessingRequest(
    string AgentId,
    CmaInput Input,
    AgentNotificationConfig AgentConfig,
    string CorrelationId,
    TaskCompletionSource<CmaWorkerResult> Completion);
```

- [ ] **Step 2: Remove storage/notification dependencies from CmaProcessingWorker**

Remove from constructor:
- `pdfGenerator` (QuestPDF) — moved to PdfWorker
- `cmaNotifier` — moved to orchestrator
- `accountConfigService` — agent config comes in request payload

Remove steps:
- `StepGeneratePdf` — deleted
- `StepNotifySeller` — deleted

Add after `StepAnalyze`:
```csharp
// Return structured result to orchestrator
var result = new CmaWorkerResult(
    ctx.Request.Input.LeadId, true, null,
    ctx.Analysis.EstimatedValue, ctx.Analysis.PriceRangeLow, ctx.Analysis.PriceRangeHigh,
    ctx.Comps.Select(c => new CompSummary(c.Address, c.Price, c.Beds, c.Baths, c.Sqft, c.DaysOnMarket, c.Distance)).ToList(),
    ctx.Analysis.MarketAnalysis);
ctx.Request.Completion.TrySetResult(result);
```

Add in the catch/failure path:
```csharp
ctx.Request.Completion.TrySetResult(new CmaWorkerResult(leadId, false, ex.Message, null, null, null, null, null));
```

- [ ] **Step 3: Update CMA worker tests**

Remove tests for PDF generation and notification steps. Add test verifying `Completion.TrySetResult` is called with correct `CmaWorkerResult`. Add test for failure path setting error result.

- [ ] **Step 4: Remove QuestPDF package reference from Workers.Cma.csproj**

Remove the `<PackageReference Include="QuestPDF" .../>` line.

- [ ] **Step 5: Verify build + tests**

```bash
dotnet build apps/api/RealEstateStar.Workers.Cma/RealEstateStar.Workers.Cma.csproj --no-restore
dotnet test apps/api/tests/RealEstateStar.Workers.Cma.Tests --no-restore --logger "console;verbosity=minimal"
```

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "refactor: CMA worker returns JSON result via TaskCompletionSource, no storage/notification"
```

---

### Task 7: Refactor HomeSearch worker — same pattern as CMA

**Files:**
- Modify: `apps/api/RealEstateStar.Workers.HomeSearch/HomeSearchProcessingWorker.cs`
- Modify: `apps/api/RealEstateStar.Workers.HomeSearch/HomeSearchProcessingChannel.cs`
- Modify: `apps/api/RealEstateStar.Workers.HomeSearch/HomeSearchPipelineContext.cs`
- Modify: `apps/api/tests/RealEstateStar.Workers.HomeSearch.Tests/` — update tests

- [ ] **Step 1: Add `TaskCompletionSource<HomeSearchWorkerResult>` to `HomeSearchProcessingRequest`**

Same pattern as Task 6 Step 1.

- [ ] **Step 2: Remove storage/notification dependencies from HomeSearchProcessingWorker**

Remove from constructor:
- `homeSearchNotifier` — moved to orchestrator
- Config lookups — agent config comes in payload

Remove step: `StepNotifyBuyer`

Add result return via `Completion.TrySetResult(new HomeSearchWorkerResult(...))`.

- [ ] **Step 3: Update HomeSearch worker tests**

Same pattern as Task 6 Step 3.

- [ ] **Step 4: Verify build + tests**

```bash
dotnet build apps/api/RealEstateStar.Workers.HomeSearch/RealEstateStar.Workers.HomeSearch.csproj --no-restore
dotnet test apps/api/tests/RealEstateStar.Workers.HomeSearch.Tests --no-restore --logger "console;verbosity=minimal"
```

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor: HomeSearch worker returns JSON result via TaskCompletionSource, no storage/notification"
```

---

### Task 8: PDF Worker

**Files:**
- Create: `apps/api/RealEstateStar.Workers.Leads/PdfWorker.cs`
- Create: `apps/api/RealEstateStar.Workers.Leads/PdfProcessingChannel.cs`
- Create: `apps/api/tests/RealEstateStar.Workers.Leads.Tests/PdfWorkerTests.cs`

- [ ] **Step 1: Create `PdfProcessingChannel`**

```csharp
namespace RealEstateStar.Workers.Leads;

public record PdfProcessingRequest(
    string LeadId,
    CmaWorkerResult CmaResult,
    AgentNotificationConfig AgentConfig,
    string CorrelationId,
    TaskCompletionSource<PdfWorkerResult> Completion);

public sealed class PdfProcessingChannel : ProcessingChannelBase<PdfProcessingRequest>
{
    public PdfProcessingChannel() : base(capacity: 20) { }
}
```

- [ ] **Step 2: Write failing tests for PdfWorker**

Test cases:
1. Generates PDF from CmaWorkerResult and writes to storage — `Completion` resolves with storage path
2. Returns failure result when PDF generation throws
3. Returns failure result when storage write throws
4. PDF file name includes lead ID and timestamp

- [ ] **Step 3: Implement `PdfWorker`**

A `BackgroundService` that reads from `Channel<PdfProcessingRequest>`. Uses QuestPDF (moved from CMA worker) to generate PDF from `CmaWorkerResult` JSON. Writes to `IFileStorageProvider`. Sets `Completion` with `PdfWorkerResult { StoragePath }`.

Add QuestPDF package reference to `Workers.Leads.csproj`.

- [ ] **Step 4: Run tests**

```bash
dotnet test apps/api/tests/RealEstateStar.Workers.Leads.Tests --filter "FullyQualifiedName~PdfWorkerTests" --no-restore
```

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add PdfWorker — generates CMA PDF from JSON, writes to storage"
```

---

## Phase 3: Orchestrator + Notifications (run Tasks 9–11 in parallel, after Phase 2)

---

### Task 9: Lead Orchestrator

**Files:**
- Create: `apps/api/RealEstateStar.Workers.Leads/LeadOrchestrator.cs`
- Create: `apps/api/RealEstateStar.Workers.Leads/LeadOrchestratorChannel.cs`
- Create: `apps/api/tests/RealEstateStar.Workers.Leads.Tests/LeadOrchestratorTests.cs`

- [ ] **Step 1: Create `LeadOrchestratorChannel`**

```csharp
namespace RealEstateStar.Workers.Leads;

public record LeadOrchestrationRequest(
    string AgentId,
    Lead Lead,
    string CorrelationId);

public sealed class LeadOrchestratorChannel : ProcessingChannelBase<LeadOrchestrationRequest>
{
    public LeadOrchestratorChannel() : base(capacity: 100) { }
}
```

- [ ] **Step 2: Write failing tests**

Test cases:
1. **Seller lead** — dispatches CMA, collects result, dispatches PDF, sends email, sends WhatsApp
2. **Buyer lead** — dispatches HomeSearch, collects result, sends email, sends WhatsApp (no PDF)
3. **Both lead** — dispatches CMA + HomeSearch in parallel, collects both, dispatches PDF, sends email with both results, sends WhatsApp
4. **CMA timeout** — proceeds without CMA, email sent without PDF, WhatsApp notes "CMA pending"
5. **HomeSearch timeout** — proceeds without listings, WhatsApp notes "Listings pending"
6. **All workers fail** — still sends WhatsApp: "analysis failed — contact manually"
7. **Checkpoint/resume** — if score checkpoint exists, skips scoring; if results checkpoint exists, skips dispatch
8. **Single storage read** — mock `ILeadStore` and `IAccountConfigService`, verify each called exactly once at entry
9. **Lead status progression** — verify status updates: Scored → Analyzing → Notified → Complete

- [ ] **Step 3: Implement `LeadOrchestrator`**

Standalone `BackgroundService`. Reads from `LeadOrchestratorChannel`. Implements:
1. Load lead + agent config (single read)
2. Score via `ILeadScorer`
3. Build `AgentNotificationConfig` from account config
4. Dispatch CMA/HomeSearch with `TaskCompletionSource` per worker
5. `Task.WhenAll` with configurable timeout
6. Dispatch PDF if CMA result present
7. Draft email via `ILeadEmailDrafter`
8. Send email via `IGmailSender`
9. Notify agent via `IAgentNotifier`
10. Save results + update status

Retry via Polly wrapping the entire orchestration. Health tracking via `BackgroundServiceHealthTracker`.

- [ ] **Step 4: Run tests**

```bash
dotnet test apps/api/tests/RealEstateStar.Workers.Leads.Tests --filter "FullyQualifiedName~LeadOrchestratorTests" --no-restore
```

- [ ] **Step 5: Run coverage**

```bash
bash apps/api/scripts/coverage.sh --low-only
```

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: add LeadOrchestrator — dispatch/collect workers, notifications, checkpoints"
```

---

### Task 10: Email drafter + HTML template

**Files:**
- Create: `apps/api/RealEstateStar.Workers.Leads/LeadEmailDrafter.cs`
- Create: `apps/api/RealEstateStar.Workers.Leads/LeadEmailTemplate.cs`
- Create: `apps/api/tests/RealEstateStar.Workers.Leads.Tests/LeadEmailDrafterTests.cs`
- Create: `apps/api/tests/RealEstateStar.Workers.Leads.Tests/LeadEmailTemplateTests.cs`

- [ ] **Step 1: Define `ILeadEmailDrafter` interface**

```csharp
public interface ILeadEmailDrafter
{
    Task<LeadEmail> DraftAsync(
        Lead lead, LeadScore score,
        CmaWorkerResult? cmaResult, HomeSearchWorkerResult? homeSearchResult,
        AgentNotificationConfig agentConfig, CancellationToken ct);
}

public record LeadEmail(string Subject, string HtmlBody, string? PdfAttachmentPath);
```

- [ ] **Step 2: Write failing tests for LeadEmailDrafter**

Test cases:
1. Calls Claude with lead data + CMA result + agent config → returns personalized + pitch paragraphs
2. Falls back to template-only when Claude fails (no personalized paragraph)
3. Subject line includes agent name and lead purpose (selling/buying)
4. Includes PDF path when CMA result available

- [ ] **Step 3: Implement `LeadEmailDrafter`**

Calls `IAnthropicClient` with a prompt that includes lead form data, CMA/HomeSearch results, and agent bio/specialties/testimonials. Returns two paragraphs: personalized and agent pitch. Falls back to empty paragraphs on failure.

- [ ] **Step 4: Write failing tests for LeadEmailTemplate**

Test cases:
1. Renders HTML with agent branding header (logo, colors)
2. Includes personalized paragraph and agent pitch
3. Includes CMA summary section when seller
4. Includes listing highlights when buyer
5. Legal footer: license number, brokerage, opt-out/CCPA/privacy links with signed tokens
6. "Powered by Real Estate Star" footnote present
7. Mobile-responsive meta viewport and max-width styles
8. Signed privacy tokens contain HMAC signature, not raw email
9. Handles missing optional fields gracefully (no BrokerageLogo, no Bio)

- [ ] **Step 5: Implement `LeadEmailTemplate`**

Builds HTML string with inline CSS (email clients don't support external stylesheets). Uses agent's `PrimaryColor`/`AccentColor` for branding. Signs privacy links with HMAC token.

- [ ] **Step 6: Run all tests**

```bash
dotnet test apps/api/tests/RealEstateStar.Workers.Leads.Tests --filter "FullyQualifiedName~EmailDrafter|FullyQualifiedName~EmailTemplate" --no-restore
```

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: add LeadEmailDrafter + LeadEmailTemplate — Claude-drafted branded lead emails"
```

---

### Task 11: Agent notifier — WhatsApp with email fallback

**Files:**
- Create: `apps/api/RealEstateStar.Workers.Leads/AgentNotifier.cs`
- Create: `apps/api/tests/RealEstateStar.Workers.Leads.Tests/AgentNotifierTests.cs`

- [ ] **Step 1: Define `IAgentNotifier` interface in Domain**

```csharp
// apps/api/RealEstateStar.Domain/Leads/Interfaces/IAgentNotifier.cs
public interface IAgentNotifier
{
    Task NotifyAsync(Lead lead, LeadScore score,
        CmaWorkerResult? cmaResult, HomeSearchWorkerResult? homeSearchResult,
        AgentNotificationConfig agentConfig, CancellationToken ct);
}
```

- [ ] **Step 2: Write failing tests**

Test cases:
1. WhatsApp configured + send succeeds → done, no email sent
2. WhatsApp not configured (null PhoneNumberId) → falls back to email
3. WhatsApp send fails → falls back to email
4. WhatsApp fails + email fails → logs error, does not throw
5. Message format: contains name, phone, email, timeline, score, bucket
6. Seller details: contains address, estimated value, comp count
7. Buyer details: contains area, price range, listing count
8. Both: contains both seller and buyer details
9. Uses `new_lead_notification` WhatsApp template name
10. Fallback email uses HTML formatting with agent branding

- [ ] **Step 3: Implement `AgentNotifier`**

```csharp
public class AgentNotifier(
    IWhatsAppSender whatsAppClient,
    IGmailSender gmailSender,
    ILogger<AgentNotifier> logger) : IAgentNotifier
{
    public async Task NotifyAsync(...)
    {
        // Try WhatsApp first
        if (!string.IsNullOrEmpty(agentConfig.WhatsAppPhoneNumberId))
        {
            try
            {
                await whatsAppClient.SendTemplateAsync(
                    agentConfig.WhatsAppPhoneNumberId,
                    "new_lead_notification",
                    BuildTemplateParameters(lead, score, cmaResult, homeSearchResult));
                return; // Success
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[AGENT-NOTIFY-001] WhatsApp failed for {AgentId}, falling back to email", agentConfig.AgentId);
            }
        }

        // Fallback: email to agent
        try
        {
            var html = BuildAgentNotificationEmail(lead, score, cmaResult, homeSearchResult, agentConfig);
            await gmailSender.SendAsync(agentConfig.AgentId, agentConfig.Email, "New Lead Notification", html, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[AGENT-NOTIFY-002] Both WhatsApp and email failed for {AgentId}", agentConfig.AgentId);
        }
    }
}
```

- [ ] **Step 4: Run tests**

```bash
dotnet test apps/api/tests/RealEstateStar.Workers.Leads.Tests --filter "FullyQualifiedName~AgentNotifierTests" --no-restore
```

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add AgentNotifier — WhatsApp template notification with Gmail fallback"
```

---

## Phase 4: Wiring + Cleanup (run Tasks 12–14 in parallel, after Phase 3)

---

### Task 12: DI wiring in Program.cs

**Files:**
- Modify: `apps/api/RealEstateStar.Api/Program.cs`
- Modify: `apps/api/RealEstateStar.Api/appsettings.json`

- [ ] **Step 1: Add `Pipeline:Lead:WorkerTimeoutSeconds` to appsettings.json**

```json
"Pipeline": {
  "Lead": {
    "WorkerTimeoutSeconds": 300,
    "Retry": { ... }
  }
}
```

- [ ] **Step 2: Replace lead pipeline DI registrations**

Remove:
```csharp
builder.Services.AddSingleton<LeadProcessingChannel>();
builder.Services.AddHostedService<LeadProcessingWorker>();
builder.Services.AddSingleton<ILeadEnricher>(sp => new ScraperLeadEnricher(...));
builder.Services.AddSingleton<ILeadNotifier, MultiChannelLeadNotifier>();
builder.Services.AddSingleton<IFailedNotificationStore>(...);
```

Add:
```csharp
builder.Services.AddSingleton<LeadOrchestratorChannel>();
builder.Services.AddSingleton<PdfProcessingChannel>();
builder.Services.AddSingleton<ILeadScorer, LeadScorer>();
builder.Services.AddSingleton<ILeadEmailDrafter, LeadEmailDrafter>();
builder.Services.AddSingleton<IAgentNotifier, AgentNotifier>();
builder.Services.AddHostedService<LeadOrchestrator>();
builder.Services.AddHostedService<PdfWorker>();
```

Update the lead submission endpoint to write to `LeadOrchestratorChannel` instead of `LeadProcessingChannel`.

- [ ] **Step 3: Verify build**

```bash
dotnet build apps/api/RealEstateStar.Api.sln --no-restore
```

- [ ] **Step 4: Run full test suite**

```bash
dotnet test apps/api/RealEstateStar.Api.sln --no-restore --logger "console;verbosity=minimal"
```

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: wire LeadOrchestrator + PdfWorker + AgentNotifier in DI"
```

---

### Task 13: Delete dead code

**Files:**
- Delete: `apps/api/RealEstateStar.Workers.Leads/ScraperLeadEnricher.cs`
- Delete: `apps/api/RealEstateStar.Workers.Leads/LeadProcessingWorker.cs`
- Delete: `apps/api/RealEstateStar.Workers.Leads/LeadPipelineContext.cs`
- Delete: `apps/api/RealEstateStar.Workers.Leads/LeadProcessingChannel.cs`
- Delete: `apps/api/RealEstateStar.Notifications/Leads/MultiChannelLeadNotifier.cs`
- Delete: `apps/api/tests/RealEstateStar.Workers.Leads.Tests/ScraperLeadEnricherTests.cs`

- [ ] **Step 1: Delete all dead files**

Delete each file listed above.

- [ ] **Step 2: Remove unused `using` statements and references**

Search for any remaining references to deleted types. Fix compilation errors.

- [ ] **Step 3: Verify build + tests**

```bash
dotnet build apps/api/RealEstateStar.Api.sln --no-restore
dotnet test apps/api/RealEstateStar.Api.sln --no-restore --logger "console;verbosity=minimal"
```

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "chore: delete ScraperLeadEnricher, LeadProcessingWorker, MultiChannelLeadNotifier + dead code"
```

---

### Task 14: Architecture tests

**Files:**
- Modify: `apps/api/tests/RealEstateStar.Architecture.Tests/DependencyTests.cs`
- Modify: `apps/api/tests/RealEstateStar.Architecture.Tests/LayerTests.cs`

- [ ] **Step 1: Update dependency rules**

Add/update `[InlineData]` entries:
- `Workers.Leads` may depend on `Domain` + `Workers.Shared` (same as before)
- `Workers.Cma` must NOT reference `Data`, `Notifications`, `DataServices` (new constraint — no storage)
- `Workers.HomeSearch` must NOT reference `Data`, `Notifications`, `DataServices` (new constraint)

- [ ] **Step 2: Add test verifying CMA worker has no IFileStorageProvider dependency**

```csharp
[Fact]
public void CmaWorker_ShouldNotReference_StorageInterfaces()
{
    // Verify Workers.Cma assembly does not reference IFileStorageProvider
}
```

- [ ] **Step 3: Add test verifying HomeSearch worker has no ILeadNotifier dependency**

Same pattern.

- [ ] **Step 4: Run architecture tests**

```bash
dotnet test apps/api/tests/RealEstateStar.Architecture.Tests --no-restore --logger "console;verbosity=minimal"
```

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "test: update architecture tests — workers must not reference storage/notification"
```

---

## Phase 5: Diagnostics (run Task 15, after Phase 4)

---

### Task 15: OrchestratorDiagnostics + Grafana dashboard

**Files:**
- Create: `apps/api/RealEstateStar.Domain/Orchestration/OrchestratorDiagnostics.cs`
- Modify: `apps/api/RealEstateStar.Api/Diagnostics/OpenTelemetryExtensions.cs`
- Modify: `infra/grafana/real-estate-star-api-dashboard.json`

- [ ] **Step 1: Create `OrchestratorDiagnostics`**

Follow the exact pattern from `LeadDiagnostics.cs`. Define:
- ActivitySource: `"RealEstateStar.Orchestrator"` v1.0.0
- All 15 counters from the spec (leads_processed, leads_completed, leads_partial, leads_failed, worker_dispatches, worker_completions, worker_timeouts, email_sent, email_failed, whatsapp_sent, whatsapp_failed, checkpoints_written, checkpoints_resumed, claude_tokens.input, claude_tokens.output)
- All 7 histograms from the spec (total_duration_ms, score_duration_ms, collect_duration_ms, pdf_duration_ms, email_draft_duration_ms, email_send_duration_ms, whatsapp_send_duration_ms)

- [ ] **Step 2: Register in OpenTelemetryExtensions.cs**

Add `.AddSource(OrchestratorDiagnostics.ServiceName)` and `.AddMeter(OrchestratorDiagnostics.ServiceName)` to both tracing and metrics.

- [ ] **Step 3: Add diagnostics calls to LeadOrchestrator**

Instrument every phase of the orchestrator with the appropriate counters, histograms, and trace spans. Use `using var span = OrchestratorDiagnostics.ActivitySource.StartActivity("orchestrator.process_lead")` pattern.

- [ ] **Step 4: Add Orchestrator row to Grafana dashboard**

Add a new row to `infra/grafana/real-estate-star-api-dashboard.json` with panels for:
- Pipeline throughput (stat panel: leads_processed rate)
- End-to-end duration (histogram panel)
- Worker dispatch vs completion rates (time series)
- Timeout and failure rates (time series)
- Email/WhatsApp delivery (stat panels)
- Claude token usage (time series)

- [ ] **Step 5: Verify build + tests**

```bash
dotnet build apps/api/RealEstateStar.Api.sln --no-restore
dotnet test apps/api/RealEstateStar.Api.sln --no-restore --logger "console;verbosity=minimal"
```

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: add OrchestratorDiagnostics — 15 counters, 7 histograms, 9 trace spans + Grafana row"
```

---

## Phase 6: Integration Test (run Task 16, after Phase 5)

---

### Task 16: End-to-end pipeline integration test

**Files:**
- Create: `apps/api/tests/RealEstateStar.Workers.Leads.Tests/LeadPipelineIntegrationTests.cs`

- [ ] **Step 1: Write integration tests**

Test cases:
1. **Full seller flow:** Submit seller lead → orchestrator scores → dispatches CMA → collects result → dispatches PDF → sends email with PDF → sends WhatsApp → status = Complete
2. **Full buyer flow:** Submit buyer lead → orchestrator scores → dispatches HomeSearch → collects result → sends email with listings → sends WhatsApp → status = Complete
3. **Full both flow:** Submit buyer+seller lead → dispatches CMA + HomeSearch in parallel → collects both → dispatches PDF → sends email with everything → sends WhatsApp → status = Complete
4. **Partial failure:** CMA times out → email sent without PDF → WhatsApp sent with "CMA pending"
5. **Resume after crash:** Checkpoint at "results collected" → restart → skips dispatch → sends notifications

Use in-memory channels, mock `IAnthropicClient`, mock `IGmailSender`, mock `IWhatsAppSender`, mock `IFileStorageProvider`, mock `IRentCastClient`.

- [ ] **Step 2: Run integration tests**

```bash
dotnet test apps/api/tests/RealEstateStar.Workers.Leads.Tests --filter "FullyQualifiedName~IntegrationTests" --no-restore
```

- [ ] **Step 3: Run full test suite one final time**

```bash
dotnet test apps/api/RealEstateStar.Api.sln --no-restore --logger "console;verbosity=minimal"
```

- [ ] **Step 4: Run coverage**

```bash
bash apps/api/scripts/coverage.sh --low-only
```

All new classes must have 100% branch coverage.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "test: add end-to-end lead pipeline integration tests"
```

---

## Post-Implementation

After all tasks are complete:
1. Open PR with all changes
2. Verify CI passes
3. Deploy to Azure Container Apps
4. Submit test lead on production
5. Verify in Grafana: orchestrator metrics flowing, trace spans visible
6. Verify agent receives WhatsApp notification (or email fallback)
7. Verify lead receives branded email with CMA PDF
