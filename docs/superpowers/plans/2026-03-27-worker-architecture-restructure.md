# Worker Architecture Restructure (Phases 5-6) -- Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the worker architecture restructure by adding content-aware smart retry, a PersistActivity for batch result writes, cross-lead content caching for spam protection, status-based gating on resubmission, and exhaustive dependency graph tests.

**Architecture:** The LeadOrchestrator (already a per-lead BackgroundService on `refactor/services-activities-cleanup`) gains SHA256 content hashing per activity, `LeadRetryState` integration for skip-on-match/re-run-on-change, a `PersistActivity` for batch upsert of all pipeline artifacts, an `IMemoryCache`-backed cross-lead dedup cache, and per-project dependency graph tests enforced in CI.

**Tech Stack:** .NET 10, SHA256, IMemoryCache, System.Text.Json, xUnit [Theory] + InlineData, NetArchTest

**Spec:** `docs/superpowers/specs/2026-03-27-worker-architecture-restructure-design.md` (Sections 7-9, Phase 5-6)

**Branch:** `refactor/services-activities-cleanup` (Phases 0-4 already merged on this branch)

---

## Spawn Pattern

```
Phase 5a: Launch Tasks 1-2 in parallel (2 agents) -- Content hash utility + PersistActivity
Phase 5b: Launch Task 3 alone (1 agent) -- Wire retry state into orchestrator (depends on Task 1)
Phase 5c: Launch Tasks 4-5 in parallel (2 agents) -- Cross-lead cache + SubmitLeadEndpoint gating (independent of each other, depend on Task 3)
Phase 6:  Launch Tasks 6-8 in parallel (3 agents) -- All architecture test tasks are independent
Post:     Launch Task 9 alone (1 agent) -- Update docs (depends on all above)
```

**Important codebase notes for implementers:**
- `LeadRetryState` already exists at `Domain/Leads/Models/LeadRetryState.cs` with `CompletedActivityKeys`, `CompletedResultPaths`, `IsCompleted(name, hash)`, and `GetHash(name)`.
- `LeadPipelineContext` already has `RetryState` property (initialized to empty `new()`).
- `CommunicationRecord` already has `ContentHash` property for dedup.
- `SubmissionCount` already exists on `Lead.cs` and engagement scoring is already wired into `LeadScorer`.
- `IDocumentStorageProvider` is in `Domain/Shared/Interfaces/Storage/` -- use this for file writes.
- `ILeadStore` is in `Domain/Leads/Interfaces/` -- use for status updates.
- Projects live under grouped folders: `apps/api/RealEstateStar.Activities/`, `apps/api/RealEstateStar.Services/`, `apps/api/RealEstateStar.Workers/`.
- Architecture tests are in `apps/api/RealEstateStar.Tests/RealEstateStar.Architecture.Tests/` with `DependencyTests.cs` (reflection) and `LayerTests.cs` (NetArchTest).

---

## File Structure

**Files to create:**
- `apps/api/RealEstateStar.Domain/Leads/Hashing/ActivityHasher.cs` -- static SHA256 hash computation per activity type
- `apps/api/RealEstateStar.Domain/Leads/Hashing/ActivityNames.cs` -- string constants for activity names
- `apps/api/RealEstateStar.Activities/RealEstateStar.Activities.Persist/PersistActivity.cs` -- batch upsert of pipeline artifacts
- `apps/api/RealEstateStar.Activities/RealEstateStar.Activities.Persist/RealEstateStar.Activities.Persist.csproj`
- `apps/api/RealEstateStar.Activities/RealEstateStar.Activities.Persist/ServiceCollectionExtensions.cs`
- `apps/api/RealEstateStar.Domain/Leads/Interfaces/IContentCache.cs` -- cross-lead dedup cache interface
- `apps/api/RealEstateStar.Workers/RealEstateStar.Workers.Lead.Orchestrator/ContentCache.cs` -- IMemoryCache-backed implementation
- `apps/api/RealEstateStar.Tests/RealEstateStar.Domain.Tests/Leads/Hashing/ActivityHasherTests.cs`
- `apps/api/RealEstateStar.Tests/RealEstateStar.Activities.Tests/Persist/PersistActivityTests.cs`
- `apps/api/RealEstateStar.Tests/RealEstateStar.Workers.Tests/Lead/Orchestrator/ContentCacheTests.cs`
- `apps/api/RealEstateStar.Tests/RealEstateStar.Workers.Tests/Lead/Orchestrator/RetryIntegrationTests.cs`
- `apps/api/RealEstateStar.Tests/RealEstateStar.Api.Tests/Features/Leads/Submit/StatusGatingTests.cs`

**Files to modify:**
- `apps/api/RealEstateStar.Workers/RealEstateStar.Workers.Lead.Orchestrator/LeadOrchestrator.cs` -- add retry checks + cache checks before each activity
- `apps/api/RealEstateStar.Workers/RealEstateStar.Workers.Lead.Orchestrator/RealEstateStar.Workers.Lead.Orchestrator.csproj` -- add Activities.Persist reference
- `apps/api/RealEstateStar.Workers/RealEstateStar.Workers.Lead.Orchestrator/ServiceCollectionExtensions.cs` -- register ContentCache + PersistActivity
- `apps/api/RealEstateStar.Api/Features/Leads/Submit/SubmitLeadEndpoint.cs` -- add status-based gating before dispatch
- `apps/api/RealEstateStar.Api/Program.cs` -- register Activities.Persist, IContentCache
- `apps/api/RealEstateStar.Api/RealEstateStar.Api.csproj` -- add Activities.Persist reference
- `apps/api/RealEstateStar.Tests/RealEstateStar.Architecture.Tests/DependencyTests.cs` -- add per-project allowed-dependency [Theory] tests
- `apps/api/RealEstateStar.Tests/RealEstateStar.Architecture.Tests/LayerTests.cs` -- mirror new constraints in NetArchTest
- `apps/api/RealEstateStar.Tests/RealEstateStar.Architecture.Tests/ProjectManifestTests.cs` -- new file: validate <Description> in all csproj files
- `apps/api/RealEstateStar.Tests/RealEstateStar.Architecture.Tests/CsprojReferenceTests.cs` -- new file: parse csproj ProjectReference tags
- All `.csproj` files for new/modified projects -- add `<Description>` element

---

## Phase 5a: Content Hash + PersistActivity (parallel -- 2 agents)

---

### Task 1: Content hash computation utility

**Files:**
- Create: `apps/api/RealEstateStar.Domain/Leads/Hashing/ActivityHasher.cs`
- Create: `apps/api/RealEstateStar.Domain/Leads/Hashing/ActivityNames.cs`
- Create: `apps/api/RealEstateStar.Tests/RealEstateStar.Domain.Tests/Leads/Hashing/ActivityHasherTests.cs`

- [ ] **Step 1: Create `ActivityNames` constants class**
  - Static class with `const string` fields: `Cma = "cma"`, `HomeSearch = "homeSearch"`, `Pdf = "pdf"`, `DraftLeadEmail = "draftLeadEmail"`, `DraftAgentNotification = "draftAgentNotification"`, `Persist = "persist"`
  - These match the keys used in `LeadRetryState.CompletedActivityKeys`

- [ ] **Step 2: Create `ActivityHasher` static class**
  - `ComputeCmaHash(SellerDetails seller)` -- SHA256 of `address|city|state|zip|notes` (null-safe, lowercase, trimmed)
  - `ComputeHomeSearchHash(BuyerDetails buyer)` -- SHA256 of `city|state|minBudget|maxBudget|beds|baths|notes`
  - `ComputePdfHash(CmaWorkerResult cmaResult)` -- SHA256 of JSON-serialized CmaWorkerResult
  - `ComputeDraftEmailHash(LeadScore score, string? cmaHash, string? hsHash)` -- SHA256 of `score.OverallScore|cmaHash|hsHash`
  - `ComputeDraftAgentNotificationHash(LeadScore score, string? cmaHash, string? hsHash)` -- same pattern as draft email
  - Private `Hash(string input)` helper using `SHA256.HashData()` returning lowercase hex string
  - All inputs are pipe-delimited, null replaced with empty string, trimmed

- [ ] **Step 3: Write exhaustive tests**
  - Same input produces same hash (deterministic)
  - Different input produces different hash
  - Null fields handled gracefully (treated as empty)
  - Whitespace trimming verified (leading/trailing spaces ignored)
  - Case insensitivity verified (addresses lowercased before hashing)
  - Notes field included in hash (changing notes changes hash)
  - Each `Compute*` method has at least 3 test cases

- [ ] **Step 4: Commit** -- `feat: add content-aware SHA256 hash computation for pipeline activities`

---

### Task 2: PersistActivity implementation

**Files:**
- Create: `apps/api/RealEstateStar.Activities/RealEstateStar.Activities.Persist/PersistActivity.cs`
- Create: `apps/api/RealEstateStar.Activities/RealEstateStar.Activities.Persist/RealEstateStar.Activities.Persist.csproj`
- Create: `apps/api/RealEstateStar.Activities/RealEstateStar.Activities.Persist/ServiceCollectionExtensions.cs`
- Create: `apps/api/RealEstateStar.Tests/RealEstateStar.Activities.Tests/Persist/PersistActivityTests.cs`
- Modify: `apps/api/RealEstateStar.Api/RealEstateStar.Api.csproj` -- add project reference
- Modify: `apps/api/RealEstateStar.Api/Program.cs` -- register PersistActivity
- Modify: solution file -- add new project

- [ ] **Step 1: Create the `.csproj` for Activities.Persist**
  - Dependencies: Domain only (follows architecture rules)
  - `<Description>Idempotent batch upsert of lead pipeline artifacts to storage</Description>`
  - Target framework: net10.0

- [ ] **Step 2: Implement `PersistActivity`**
  - Constructor takes `ILeadStore` and `IDocumentStorageProvider`
  - `ExecuteAsync(LeadPipelineContext ctx, CancellationToken ct)` method
  - Writes to lead folder via `LeadPaths.LeadFolder(ctx.Lead.FullName)`:
    1. CMA Summary.md -- upsert if `ctx.CmaResult?.Success == true`, render as markdown with YAML frontmatter
    2. HomeSearch Summary.md -- upsert if `ctx.HsResult?.Success == true`
    3. Lead Email Draft.md -- upsert with dedup: read existing, check `content_hash` + `sent`, skip if match
    4. Agent Notification Draft.md -- upsert with dedup (same logic)
    5. Retry State.json -- always overwrite with `JsonSerializer.Serialize(ctx.RetryState)`
  - Does NOT write PDF (PdfActivity handles that directly)
  - Does NOT update lead status (inline status writes handle that)
  - Each communication upsert checks: if existing file has same `content_hash` AND `sent: true`, skip write

- [ ] **Step 3: Implement markdown rendering helpers**
  - `RenderCmaSummary(CmaWorkerResult)` -- YAML frontmatter with estimated_value, price_range, comp_count + markdown body with market analysis
  - `RenderHomeSearchSummary(HomeSearchWorkerResult)` -- YAML frontmatter with listing_count, area + markdown body with listing details
  - `RenderCommunicationRecord(CommunicationRecord)` -- YAML frontmatter with subject, channel, content_hash, sent, drafted_at, sent_at + markdown body with HtmlBody

- [ ] **Step 4: Create `ServiceCollectionExtensions`**
  - `AddPersistActivity(this IServiceCollection services)` registers `PersistActivity` as singleton

- [ ] **Step 5: Write tests with InMemoryFileProvider**
  - Verify CMA summary written when CmaResult is successful
  - Verify CMA summary NOT written when CmaResult is null/failed
  - Verify HomeSearch summary written when HsResult is successful
  - Verify communication dedup: same hash + sent = skip
  - Verify communication overwrite: same hash + not sent = write (retry after send failure)
  - Verify communication overwrite: different hash = write (content changed)
  - Verify Retry State.json always overwritten
  - Verify PDF path is NOT written (PersistActivity doesn't own PDF)
  - Verify idempotency: calling twice with same ctx produces same files

- [ ] **Step 6: Add project reference to Api.csproj and register in Program.cs**

- [ ] **Step 7: Commit** -- `feat: add PersistActivity for idempotent batch upsert of pipeline artifacts`

---

## Phase 5b: Wire Retry State into Orchestrator (depends on Task 1)

---

### Task 3: Wire LeadRetryState into orchestrator

**Files:**
- Modify: `apps/api/RealEstateStar.Workers/RealEstateStar.Workers.Lead.Orchestrator/LeadOrchestrator.cs`
- Modify: `apps/api/RealEstateStar.Workers/RealEstateStar.Workers.Lead.Orchestrator/RealEstateStar.Workers.Lead.Orchestrator.csproj`
- Create: `apps/api/RealEstateStar.Tests/RealEstateStar.Workers.Tests/Lead/Orchestrator/RetryIntegrationTests.cs`

- [ ] **Step 1: Add ActivityHasher usage to orchestrator**
  - Import `RealEstateStar.Domain.Leads.Hashing`
  - Before CMA dispatch: compute `ActivityHasher.ComputeCmaHash(lead.SellerDetails)`, check `ctx.RetryState.IsCompleted(ActivityNames.Cma, hash)` -- if true, load cached result from `CompletedResultPaths`, skip dispatch
  - Before HomeSearch dispatch: compute `ActivityHasher.ComputeHomeSearchHash(lead.BuyerDetails)`, same check/skip pattern
  - Before PDF generation: compute `ActivityHasher.ComputePdfHash(ctx.CmaResult)`, same check/skip
  - Before email draft: compute `ActivityHasher.ComputeDraftEmailHash(ctx.Score, cmaHash, hsHash)`, same check/skip
  - Before agent notification draft: same pattern

- [ ] **Step 2: Add retry state recording after each activity**
  - After CMA completes: `ctx.RetryState.CompletedActivityKeys[ActivityNames.Cma] = cmaHash`
  - Store serialized CmaWorkerResult path in `ctx.RetryState.CompletedResultPaths[ActivityNames.Cma]`
  - Same pattern for HomeSearch, PDF, DraftEmail, DraftAgentNotification
  - The PersistActivity (Task 2) writes the final RetryState.json

- [ ] **Step 3: Add Activities.Persist reference and call PersistActivity at end of pipeline**
  - Add `PersistActivity` to orchestrator constructor (DI)
  - After all activities complete (Step 9 in current code), call `await persistActivity.ExecuteAsync(ctx, ct)`
  - This is the final step before `UpdateStatusAsync(Complete)`

- [ ] **Step 4: Add retry state loading on orchestrator start**
  - When `ProcessRequestAsync` begins, check if lead's Retry State.json exists via `IDocumentStorageProvider`
  - If it exists, deserialize into `ctx.RetryState`
  - If not, `ctx.RetryState` stays as empty `new()` (first run)

- [ ] **Step 5: Add OTel span tags for cache hits/misses**
  - On each activity span: add `cache.hit = true/false` tag
  - Add counter `orchestrator.retry_cache_hits` and `orchestrator.retry_cache_misses`

- [ ] **Step 6: Write retry integration tests**
  - First run: all activities execute, retry state saved
  - Second run with same input: all activities skipped (hashes match)
  - Second run with different seller address: CMA re-runs, HomeSearch skips
  - Second run with different buyer criteria: HomeSearch re-runs, CMA skips
  - CMA re-run causes PDF and email draft to re-run (downstream hash changes)
  - Timeout scenario: partial retry state saved, next run completes remaining steps
  - Verify retry state JSON written to storage

- [ ] **Step 7: Commit** -- `feat: wire content-aware retry state into LeadOrchestrator`

---

## Phase 5c: Cross-Lead Cache + Status Gating (parallel -- 2 agents, depend on Task 3)

---

### Task 4: Content-addressed dedup cache (cross-lead spam protection)

**Files:**
- Create: `apps/api/RealEstateStar.Domain/Leads/Interfaces/IContentCache.cs`
- Create: `apps/api/RealEstateStar.Workers/RealEstateStar.Workers.Lead.Orchestrator/ContentCache.cs`
- Modify: `apps/api/RealEstateStar.Workers/RealEstateStar.Workers.Lead.Orchestrator/LeadOrchestrator.cs`
- Modify: `apps/api/RealEstateStar.Workers/RealEstateStar.Workers.Lead.Orchestrator/ServiceCollectionExtensions.cs`
- Create: `apps/api/RealEstateStar.Tests/RealEstateStar.Workers.Tests/Lead/Orchestrator/ContentCacheTests.cs`

- [ ] **Step 1: Define `IContentCache` interface in Domain**
  - `TryGet<T>(string key, out T? value)` -- returns true if cached, false if miss
  - `Set<T>(string key, T value, TimeSpan ttl)` -- cache with expiration
  - Interface lives in `Domain/Leads/Interfaces/` since it's used by orchestrator (Domain dependency)

- [ ] **Step 2: Implement `ContentCache` using IMemoryCache**
  - Constructor takes `IMemoryCache`
  - `TryGet<T>` delegates to `_cache.TryGetValue`
  - `Set<T>` delegates to `_cache.Set` with `MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }`
  - Cache key format: `"activity:{activityName}:{contentHash}"` (namespaced to avoid collisions)

- [ ] **Step 3: Wire into orchestrator**
  - Add `IContentCache` to orchestrator constructor
  - Before CMA dispatch: check `cache.TryGet<CmaWorkerResult>(cmaKey, out cached)` -- if hit, use cached result, skip dispatch entirely
  - After CMA completes: `cache.Set(cmaKey, result, TimeSpan.FromHours(24))`
  - Before HomeSearch dispatch: same check with 1-hour TTL
  - After HomeSearch completes: `cache.Set(hsKey, result, TimeSpan.FromHours(1))`
  - PDF cache: 24-hour TTL keyed on CmaWorkerResult hash
  - Notifications: NOT cached (always send -- spec says so)
  - Cache check happens BEFORE per-lead retry check (shared cache is cheaper than file I/O)

- [ ] **Step 4: Add OTel metrics for cache**
  - `content_cache.hit` counter (tagged by activity name)
  - `content_cache.miss` counter (tagged by activity name)
  - Span tag `cross_lead_cache.hit = true/false` on each activity span

- [ ] **Step 5: Register in ServiceCollectionExtensions**
  - `services.AddMemoryCache()` (if not already registered)
  - `services.AddSingleton<IContentCache, ContentCache>()`

- [ ] **Step 6: Write tests**
  - Cache miss on first call, returns false
  - Cache hit on second call with same key, returns cached value
  - Different key = cache miss (different address)
  - TTL expiration: set with short TTL, verify miss after expiration
  - CMA 24hr TTL vs HomeSearch 1hr TTL verified
  - Cross-lead scenario: Lead A computes CMA for "123 Main St", Lead B requests same address = cache hit
  - Cache does NOT apply to notifications (verify always-send behavior)

- [ ] **Step 7: Commit** -- `feat: add cross-lead content cache for CMA/HomeSearch dedup`

---

### Task 5: Update SubmitLeadEndpoint for status-based gating

**Files:**
- Modify: `apps/api/RealEstateStar.Api/Features/Leads/Submit/SubmitLeadEndpoint.cs`
- Create: `apps/api/RealEstateStar.Tests/RealEstateStar.Api.Tests/Features/Leads/Submit/StatusGatingTests.cs`

- [ ] **Step 1: Add status check before orchestrator dispatch**
  - After dedup check (existing lead found), read current `lead.Status`
  - `Received` -- dispatch to orchestrator (normal flow)
  - `Scored` or `Analyzing` -- already in progress, return `202 Accepted` with message "Lead is already being processed"
  - `Notified` -- already in progress (notification phase), return `202 Accepted`
  - `Complete` -- check content hash:
    - Compute new content hash from request (seller address + buyer criteria)
    - Compare with stored retry state hash
    - If same -- return `200 OK` with message "Lead already processed with same data"
    - If different -- increment `SubmissionCount`, re-dispatch to orchestrator (retry with new data)

- [ ] **Step 2: Add content hash comparison helper**
  - Use `ActivityHasher.ComputeCmaHash` and `ActivityHasher.ComputeHomeSearchHash` from Task 1
  - Compare against `lead.RetryState.GetHash(ActivityNames.Cma)` and `lead.RetryState.GetHash(ActivityNames.HomeSearch)`
  - If either hash differs, the lead has new data and should be re-dispatched

- [ ] **Step 3: Add structured logging for gating decisions**
  - `[SubmitLead-020] Lead {LeadId} already in progress (status: {Status}), skipping dispatch`
  - `[SubmitLead-021] Lead {LeadId} resubmitted with same data, returning cached result`
  - `[SubmitLead-022] Lead {LeadId} resubmitted with changed data, re-dispatching (submission #{Count})`

- [ ] **Step 4: Add OTel counter for gating outcomes**
  - `lead.submit.dispatched` -- new lead or changed data
  - `lead.submit.skipped_in_progress` -- already processing
  - `lead.submit.skipped_unchanged` -- complete with same data

- [ ] **Step 5: Write tests**
  - New lead (no existing) -- dispatches normally, returns 202
  - Existing lead with status `Received` -- dispatches, returns 202
  - Existing lead with status `Scored` -- returns 202 "already processing"
  - Existing lead with status `Analyzing` -- returns 202 "already processing"
  - Existing lead with status `Notified` -- returns 202 "already processing"
  - Existing lead with status `Complete` + same data -- returns 200 "already processed"
  - Existing lead with status `Complete` + different seller address -- re-dispatches, returns 202
  - Existing lead with status `Complete` + different buyer criteria -- re-dispatches, returns 202
  - Verify `SubmissionCount` incremented on re-dispatch of complete lead

- [ ] **Step 6: Commit** -- `feat: add status-based gating to SubmitLeadEndpoint for dedup + resubmission`

---

## Phase 6: Exhaustive Dependency Graph Tests (parallel -- 3 agents)

---

### Task 6: Per-project allowed-dependency test

**Files:**
- Modify: `apps/api/RealEstateStar.Tests/RealEstateStar.Architecture.Tests/DependencyTests.cs`

- [ ] **Step 1: Add `[Theory]` test with `[InlineData]` per project**
  - Each `[InlineData]` specifies: project assembly name, allowed dependency assembly names (comma-separated)
  - Example: `[InlineData("RealEstateStar.Activities.Pdf", "RealEstateStar.Domain")]`
  - Example: `[InlineData("RealEstateStar.Workers.Lead.Orchestrator", "RealEstateStar.Domain,RealEstateStar.Workers.Shared,RealEstateStar.Workers.Lead.CMA,RealEstateStar.Workers.Lead.HomeSearch,RealEstateStar.Activities.Pdf,RealEstateStar.Activities.Persist,RealEstateStar.Services.AgentNotifier,RealEstateStar.Services.LeadCommunicator")]`
  - Cover ALL projects in the solution (not just worker projects)

- [ ] **Step 2: Implement the test method**
  - Load assembly by name
  - Get all referenced assemblies (`assembly.GetReferencedAssemblies()`)
  - Filter to only `RealEstateStar.*` references
  - Assert each reference is in the allowed list
  - Fail with descriptive message: `"{project} references {illegal} which is not in allowed list: [{allowed}]"`

- [ ] **Step 3: Verify existing constraints still hold**
  - Domain references nothing (no RealEstateStar.* refs)
  - Data references Domain only
  - Clients.* reference Domain only
  - DataServices references Domain only
  - Workers.Shared references Domain only
  - Activities.Pdf references Domain + Workers.Shared
  - Activities.Persist references Domain only
  - Services.AgentNotifier references Domain + Workers.Shared
  - Services.LeadCommunicator references Domain + Workers.Shared
  - Workers.Lead.CMA references Domain + Workers.Shared
  - Workers.Lead.HomeSearch references Domain + Workers.Shared
  - Workers.Lead.Orchestrator references Domain + Workers.Shared + all activity/service/worker projects

- [ ] **Step 4: Commit** -- `test: add per-project allowed-dependency [Theory] tests`

---

### Task 7: csproj reference audit test

**Files:**
- Create: `apps/api/RealEstateStar.Tests/RealEstateStar.Architecture.Tests/CsprojReferenceTests.cs`

- [ ] **Step 1: Implement csproj parser**
  - Find all `.csproj` files under `apps/api/` (excluding `tests/`, `bin/`, `obj/`)
  - For each csproj, parse `<ProjectReference Include="...">` elements using `XDocument`
  - Extract the project name from the path (e.g., `..\..\RealEstateStar.Domain\RealEstateStar.Domain.csproj` -> `RealEstateStar.Domain`)

- [ ] **Step 2: Define allowed references map**
  - Same allowed-list as Task 6 but verified at csproj level (catches refs before types are used)
  - Use a `Dictionary<string, HashSet<string>>` mapping project name to allowed project references

- [ ] **Step 3: Implement `[Theory]` test**
  - For each csproj, verify all `ProjectReference` entries point to allowed projects
  - Fail with: `"{project}.csproj references {illegal}.csproj which violates dependency rules"`

- [ ] **Step 4: Test for circular references**
  - Build a directed graph from all csproj references
  - Run topological sort -- if it fails, there's a cycle
  - Fail with: `"Circular dependency detected: {cycle path}"`

- [ ] **Step 5: Commit** -- `test: add csproj ProjectReference audit tests`

---

### Task 8: Single-purpose project manifest

**Files:**
- Create: `apps/api/RealEstateStar.Tests/RealEstateStar.Architecture.Tests/ProjectManifestTests.cs`
- Modify: all `.csproj` files missing `<Description>` -- add the element

- [ ] **Step 1: Implement `<Description>` validation test**
  - Find all `.csproj` files under `apps/api/` (excluding `tests/`)
  - Parse each for `<Description>` element in `<PropertyGroup>`
  - Assert it exists and is non-empty
  - Fail with: `"{project}.csproj is missing <Description> in PropertyGroup"`

- [ ] **Step 2: Add `<Description>` to all production csproj files**
  - `RealEstateStar.Domain` -- "Pure domain models, interfaces, enums -- zero external dependencies"
  - `RealEstateStar.Data` -- "Physical file storage providers (local, in-memory)"
  - `RealEstateStar.DataServices` -- "Storage orchestration and lead data access"
  - `RealEstateStar.Notifications` -- "Notification delivery channels (email, WhatsApp)"
  - `RealEstateStar.Workers.Shared` -- "Pipeline base classes, channels, health tracking"
  - `RealEstateStar.Workers.Lead.Orchestrator` -- "Per-lead pipeline orchestrator with retry and caching"
  - `RealEstateStar.Workers.Lead.CMA` -- "Comparative Market Analysis worker (RentCast + Claude)"
  - `RealEstateStar.Workers.Lead.HomeSearch` -- "Home search worker (scraper + Claude parsing)"
  - `RealEstateStar.Workers.WhatsApp` -- "WhatsApp message processing worker"
  - `RealEstateStar.Activities.Pdf` -- "QuestPDF CMA report generation activity"
  - `RealEstateStar.Activities.Persist` -- "Idempotent batch upsert of pipeline artifacts"
  - `RealEstateStar.Services.AgentNotifier` -- "Agent notification via WhatsApp with email fallback"
  - `RealEstateStar.Services.LeadCommunicator` -- "Lead email drafting and sending via Gmail"
  - `RealEstateStar.Api` -- "HTTP layer and sole DI composition root"
  - All `Clients.*` projects -- appropriate descriptions per client

- [ ] **Step 3: Verify test passes with all descriptions added**

- [ ] **Step 4: Commit** -- `chore: add <Description> to all csproj files + manifest validation test`

---

## Post-Implementation: Documentation Updates

---

### Task 9: Update documentation

**Files:**
- Modify: `.claude/CLAUDE.md` -- update monorepo structure with Activities.Persist, content cache, retry state
- Modify: `docs/architecture/README.md` -- update dependency diagram (if exists)

- [ ] **Step 1: Update CLAUDE.md monorepo structure**
  - Add `RealEstateStar.Activities.Persist/` to the project listing
  - Update the API dependency rules section to include Activities.Persist
  - Add note about content cache and retry state pattern

- [ ] **Step 2: Update architecture diagrams**
  - Add Activities.Persist to dependency diagram
  - Add content cache flow to pipeline diagram
  - Add retry state flow to orchestrator lifecycle diagram

- [ ] **Step 3: Update onboarding docs if they reference old project structure**

- [ ] **Step 4: Commit** -- `docs: update architecture docs for smart retry + persist activity`
