# OAuth Authorization Link + Activation Pipeline — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a secure OAuth link that activates agents — collecting Google tokens, analyzing their emails/Drive/web presence, and generating a complete agent profile (voice, personality, branding, coaching) that powers personalized lead communications.

**Architecture:** 5-phase orchestrator pipeline following the project's Worker/Activity/Service hierarchy. Workers are pure compute (Clients only), Activities persist (DataServices + Services), Services handle business logic (Clients + DataServices). All new projects conform to existing architecture tests — no arch test changes.

**Tech Stack:** .NET 10, HMAC-SHA256, Google OAuth 2.0, Gmail API, Drive API, Claude API (Sonnet 4.6), Azure Table Storage, DPAPI encryption, Channel\<T\> fan-out, QuestPDF

**Spec:** `docs/superpowers/specs/2026-03-29-oauth-authorization-link-design.md`

**Demo deadline:** Wednesday 2026-04-02

---

## Parallelization Strategy

This plan is designed for **6-8 concurrent subagents**. Tasks are grouped into streams that can execute independently after the foundation is laid.

```
Stream A: Foundation (Domain + project scaffolding)     ← MUST GO FIRST
    ↓ (all streams depend on A completing)
Stream B: OAuth Link Endpoints ──────────────────────── can run in parallel
Stream C: Phase 1 Gather Workers ────────────────────── can run in parallel
Stream D: Phase 2 Synthesis Workers (batch 1) ───────── can run in parallel
Stream E: Phase 2 Synthesis Workers (batch 2) ───────── can run in parallel
Stream F: Activities + Services ─────────────────────── can run in parallel
Stream G: Orchestrator ──────────────────────────────── after C, D, E, F
Stream H: Lead Pipeline Integration ─────────────────── after A (AgentContext)
```

## Demo-Critical Path

For Wednesday's demo, these are **must-have**:
1. ✅ OAuth link generation + auth flow (Stream B)
2. ✅ At least 3 gather workers working (Stream C)
3. ✅ At least Voice + Personality + Branding workers (Stream D)
4. ✅ Orchestrator running end-to-end (Stream G)
5. ✅ PersistAgentProfile + AgentConfigService generating account.json (Stream F)

**Nice-to-have for demo** (can be stubbed):
- Welcome notification (can log instead of send)
- Brand merge (can skip merge, just create)
- Compliance/Fee workers (can skip entirely)
- Lead pipeline integration (Stream H)

---

## Stream A: Foundation (MUST COMPLETE FIRST)

### Task A1: Domain Models

**Files:**
- Create: `apps/api/RealEstateStar.Domain/Activation/Models/ActivationRequest.cs`
- Create: `apps/api/RealEstateStar.Domain/Activation/Models/AgentContext.cs`
- Create: `apps/api/RealEstateStar.Domain/Activation/Models/DriveIndex.cs`
- Create: `apps/api/RealEstateStar.Domain/Activation/Models/EmailCorpus.cs`
- Create: `apps/api/RealEstateStar.Domain/Activation/Models/AgentDiscovery.cs`
- Create: `apps/api/RealEstateStar.Domain/Activation/Models/BrandingKit.cs`
- Create: `apps/api/RealEstateStar.Domain/Activation/Models/ThirdPartyProfiles.cs`

- [ ] **Step 1: Create ActivationRequest record**

```csharp
namespace RealEstateStar.Domain.Activation.Models;

public sealed record ActivationRequest(
    string AccountId,
    string AgentId,
    string Email,
    DateTime Timestamp);
```

- [ ] **Step 2: Create AgentContext record**

```csharp
namespace RealEstateStar.Domain.Activation.Models;

public sealed record AgentContext
{
    // Per-agent skills
    public string? VoiceSkill { get; init; }
    public string? PersonalitySkill { get; init; }
    public string? CmaStyleGuide { get; init; }
    public string? MarketingStyle { get; init; }
    public string? SalesPipeline { get; init; }
    public string? CoachingReport { get; init; }
    public string? WebsiteStyleGuide { get; init; }
    public string? BrandingKit { get; init; }
    public string? ComplianceAnalysis { get; init; }
    public string? FeeStructure { get; init; }

    // Per-brokerage brand
    public string? BrandProfile { get; init; }
    public string? BrandVoice { get; init; }

    // Metadata
    public bool IsActivated { get; init; }
    public bool IsLowConfidence { get; init; }
}
```

- [ ] **Step 3: Create DriveIndex, EmailCorpus, AgentDiscovery, BrandingKit, ThirdPartyProfiles records**

These are the data transfer types between orchestrator phases. Each is a sealed record in `Domain/Activation/Models/`.

- [ ] **Step 4: Build to verify Domain compiles**

Run: `dotnet build apps/api/RealEstateStar.Domain/RealEstateStar.Domain.csproj`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add apps/api/RealEstateStar.Domain/Activation/
git commit -m "feat: add activation pipeline Domain models"
```

### Task A2: Domain Interfaces

**Files:**
- Create: `apps/api/RealEstateStar.Domain/Activation/Interfaces/IAgentContextLoader.cs`
- Create: `apps/api/RealEstateStar.Domain/Shared/Interfaces/External/IGmailReader.cs`
- Create: `apps/api/RealEstateStar.Domain/Shared/Interfaces/IContentSanitizer.cs`

- [ ] **Step 1: Create IAgentContextLoader**

```csharp
namespace RealEstateStar.Domain.Activation.Interfaces;

public interface IAgentContextLoader
{
    Task<AgentContext?> LoadAsync(string accountId, string agentId, CancellationToken ct);
}
```

- [ ] **Step 2: Create IGmailReader**

```csharp
namespace RealEstateStar.Domain.Shared.Interfaces.External;

public interface IGmailReader
{
    Task<IReadOnlyList<EmailMessage>> GetSentEmailsAsync(
        string accountId, string agentId, int maxResults, CancellationToken ct);
    Task<IReadOnlyList<EmailMessage>> GetInboxEmailsAsync(
        string accountId, string agentId, int maxResults, CancellationToken ct);
}
```

- [ ] **Step 3: Create IContentSanitizer**

```csharp
namespace RealEstateStar.Domain.Shared.Interfaces;

public interface IContentSanitizer
{
    string Sanitize(string untrustedContent);
}
```

- [ ] **Step 4: Create EmailMessage record in Domain**

```csharp
namespace RealEstateStar.Domain.Activation.Models;

public sealed record EmailMessage(
    string Id,
    string Subject,
    string Body,
    string From,
    string[] To,
    DateTime Date,
    string? SignatureBlock);
```

- [ ] **Step 5: Build + commit**

Run: `dotnet build apps/api/RealEstateStar.Domain/RealEstateStar.Domain.csproj`

```bash
git add apps/api/RealEstateStar.Domain/
git commit -m "feat: add activation pipeline Domain interfaces"
```

### Task A3: Scaffold All New Projects

**Files:**
- Create: 16 worker csproj + class files
- Create: 2 activity csproj + class files
- Create: 3 service csproj + class files
- Modify: `apps/api/RealEstateStar.Api.sln`

This task creates empty project shells with correct dependencies. Each project gets a minimal class that compiles but does nothing yet.

- [ ] **Step 1: Create all 16 worker projects**

For each worker project, create a csproj with deps on Domain + Workers.Shared:

```bash
# Example for one worker — repeat for all 16
cd apps/api/RealEstateStar.Workers/Activation
dotnet new classlib -n RealEstateStar.Workers.Activation.EmailFetch
cd RealEstateStar.Workers.Activation.EmailFetch
dotnet add reference ../../../RealEstateStar.Domain/RealEstateStar.Domain.csproj
dotnet add reference ../../RealEstateStar.Workers.Shared/RealEstateStar.Workers.Shared.csproj
```

Worker projects to create:
1. `Workers.Activation.Orchestrator`
2. `Workers.Activation.EmailFetch`
3. `Workers.Activation.DriveIndex`
4. `Workers.Activation.AgentDiscovery`
5. `Workers.Activation.VoiceExtraction`
6. `Workers.Activation.Personality`
7. `Workers.Activation.CmaStyle`
8. `Workers.Activation.MarketingStyle`
9. `Workers.Activation.WebsiteStyle`
10. `Workers.Activation.PipelineAnalysis`
11. `Workers.Activation.Coaching`
12. `Workers.Activation.BrandExtraction`
13. `Workers.Activation.BrandVoice`
14. `Workers.Activation.BrandingDiscovery`
15. `Workers.Activation.ComplianceAnalysis`
16. `Workers.Activation.FeeStructure`

- [ ] **Step 2: Create 2 activity projects**

Activities depend on Domain + Workers.Shared (for base classes):

1. `Activities.Activation.PersistAgentProfile`
2. `Activities.Activation.BrandMerge`

- [ ] **Step 3: Create 3 service projects**

Services depend on Domain only:

1. `Services.AgentConfig`
2. `Services.BrandMerge`
3. `Services.WelcomeNotification`

- [ ] **Step 4: Add all projects to solution**

```bash
cd apps/api
dotnet sln add RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.Orchestrator/RealEstateStar.Workers.Activation.Orchestrator.csproj
# ... repeat for all 21 projects
```

- [ ] **Step 5: Build entire solution**

Run: `dotnet build apps/api/RealEstateStar.Api.sln`
Expected: Build succeeded (all projects compile with empty stubs)

- [ ] **Step 6: Run architecture tests**

Run: `dotnet test apps/api/tests/RealEstateStar.Architecture.Tests/ --filter "Category=Architecture"`
Expected: All pass — new projects have correct dependency structure

- [ ] **Step 7: Commit**

```bash
git add apps/api/
git commit -m "feat: scaffold 21 activation pipeline projects with correct architecture deps"
```

### Task A4: Configuration + DI Wiring

**Files:**
- Modify: `apps/api/RealEstateStar.Api/appsettings.json`
- Modify: `apps/api/RealEstateStar.Api/Program.cs`
- Modify: `apps/api/RealEstateStar.Api/Diagnostics/OpenTelemetryExtensions.cs`

- [ ] **Step 1: Add OAuthLink config to appsettings.json**

```json
{
  "OAuthLink": {
    "Secret": "",
    "ExpirationHours": 24
  }
}
```

- [ ] **Step 2: Add startup validation in Program.cs**

```csharp
var oauthLinkSecret = builder.Configuration["OAuthLink:Secret"]
    ?? throw new InvalidOperationException("OAuthLink:Secret configuration is required");
if (oauthLinkSecret.Length < 32)
    throw new InvalidOperationException("OAuthLink:Secret must be at least 32 characters");
```

- [ ] **Step 3: Register Channel\<ActivationRequest\> in Program.cs**

```csharp
builder.Services.AddSingleton(Channel.CreateUnbounded<ActivationRequest>());
builder.Services.AddSingleton(sp => sp.GetRequiredService<Channel<ActivationRequest>>().Reader);
builder.Services.AddSingleton(sp => sp.GetRequiredService<Channel<ActivationRequest>>().Writer);
```

- [ ] **Step 4: Register rate limit policies**

```csharp
options.AddPolicy("oauth-link-generate", context =>
    RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromHours(1) }));

options.AddPolicy("oauth-link-authorize", context =>
    RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromHours(1) }));
```

- [ ] **Step 5: Add gmail.readonly scope to GoogleOAuthService**

Modify `apps/api/RealEstateStar.Api/Features/Onboarding/Services/GoogleOAuthService.cs`:

Add `"https://www.googleapis.com/auth/gmail.readonly"` and `"https://www.googleapis.com/auth/drive.readonly"` to the scopes array.

- [ ] **Step 6: Register OpenTelemetry sources**

In `OpenTelemetryExtensions.cs`:
```csharp
.AddSource("RealEstateStar.Activation")
.AddSource("RealEstateStar.AgentContext")
.AddMeter("RealEstateStar.Activation")
.AddMeter("RealEstateStar.AgentContext")
```

- [ ] **Step 7: Register health checks**

```csharp
.AddCheck<OAuthLinkSecretHealthCheck>("oauth_link_secret", tags: ["ready"])
```

- [ ] **Step 8: Build + test + commit**

Run: `dotnet build apps/api/RealEstateStar.Api.sln`
Run: `dotnet test apps/api/tests/RealEstateStar.Architecture.Tests/`

```bash
git add apps/api/RealEstateStar.Api/
git commit -m "feat: add activation pipeline config, DI, rate limits, telemetry"
```

---

## Stream B: OAuth Link Endpoints (after Stream A)

### Task B1: AuthorizationLinkService

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/OAuth/Services/AuthorizationLinkService.cs`
- Create: `apps/api/tests/RealEstateStar.Api.Tests/Features/OAuth/Services/AuthorizationLinkServiceTests.cs`

- [ ] **Step 1: Write tests for HMAC signing + validation**

```csharp
public class AuthorizationLinkServiceTests
{
    [Fact]
    public void GenerateLink_ValidParams_ProducesVerifiableSignature()
    {
        var service = CreateService("test-secret-that-is-at-least-32-chars");
        var url = service.GenerateLink("acme", "jane", "jane@test.com");
        var isValid = service.ValidateSignature(
            "acme", "jane", "jane@test.com", url.Expiry, url.Signature);
        Assert.True(isValid);
    }

    [Fact]
    public void ValidateSignature_TamperedAccountId_ReturnsFalse()
    {
        var service = CreateService("test-secret-that-is-at-least-32-chars");
        var url = service.GenerateLink("acme", "jane", "jane@test.com");
        var isValid = service.ValidateSignature(
            "evil", "jane", "jane@test.com", url.Expiry, url.Signature);
        Assert.False(isValid);
    }

    [Fact]
    public void ValidateSignature_ExpiredLink_ReturnsFalse() { /* ... */ }

    [Fact]
    public void GenerateNonce_ReturnsHex32Chars() { /* ... */ }

    [Fact]
    public void ValidateNonce_CorrectNonce_ReturnsTrue() { /* ... */ }

    [Fact]
    public void ValidateNonce_SingleUse_SecondAttemptFails() { /* ... */ }

    [Fact]
    public void ValidateNonce_Expired_ReturnsFalse() { /* ... */ }
}
```

- [ ] **Step 2: Run tests — verify they fail**

- [ ] **Step 3: Implement AuthorizationLinkService**

HMAC signing, nonce cache with ConcurrentDictionary, 10-min TTL, cleanup timer.

- [ ] **Step 4: Run tests — verify they pass**

- [ ] **Step 5: Commit**

### Task B2: GenerateAuthLinkEndpoint

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/OAuth/GenerateLink/GenerateAuthLinkEndpoint.cs`
- Create: `apps/api/tests/RealEstateStar.Api.Tests/Features/OAuth/GenerateLink/GenerateAuthLinkEndpointTests.cs`

- [ ] **Step 1: Write endpoint tests** (valid params → URL, missing params → 400)
- [ ] **Step 2: Run tests — verify they fail**
- [ ] **Step 3: Implement endpoint** (REPR pattern, rate limited)
- [ ] **Step 4: Run tests — verify they pass**
- [ ] **Step 5: Commit**

### Task B3: AuthorizeLinkEndpoint (Landing Page)

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/OAuth/AuthorizeLink/AuthorizeLinkEndpoint.cs`
- Create: `apps/api/tests/RealEstateStar.Api.Tests/Features/OAuth/AuthorizeLink/AuthorizeLinkEndpointTests.cs`

- [ ] **Step 1: Write tests** (valid sig → HTML, invalid sig → 401, expired → 410)
- [ ] **Step 2: Run tests — fail**
- [ ] **Step 3: Implement** (validate HMAC, return branded HTML with "Connect" button)
- [ ] **Step 4: Run tests — pass**
- [ ] **Step 5: Commit**

### Task B4: ConnectEndpoint + CallbackEndpoint

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/OAuth/AuthorizeLink/ConnectEndpoint.cs`
- Create: `apps/api/RealEstateStar.Api/Features/OAuth/AuthorizeLink/AuthorizeLinkCallbackEndpoint.cs`
- Create: tests for both

- [ ] **Step 1: Write ConnectEndpoint tests** (generates nonce, redirects to Google)
- [ ] **Step 2: Write CallbackEndpoint tests** (valid nonce → token stored, email mismatch → error, enqueues ActivationRequest)
- [ ] **Step 3: Run tests — fail**
- [ ] **Step 4: Implement ConnectEndpoint** (re-validate HMAC, generate nonce, store in cache, redirect)
- [ ] **Step 5: Implement CallbackEndpoint** (validate nonce, exchange code via GoogleOAuthService, verify email, store token, enqueue activation, show success HTML)
- [ ] **Step 6: Run tests — pass**
- [ ] **Step 7: Commit**

---

## Stream C: Phase 1 Gather Workers (after Stream A)

### Task C1: GmailReaderClient

**Files:**
- Create: `apps/api/RealEstateStar.Clients/RealEstateStar.Clients.Gmail/GmailReaderClient.cs`
- Create: `apps/api/tests/RealEstateStar.Clients.Gmail.Tests/GmailReaderClientTests.cs`

- [ ] **Step 1: Write tests** (fetches sent emails, fetches inbox, handles empty mailbox)
- [ ] **Step 2: Implement GmailReaderClient** (uses IOAuthRefresher + GoogleCredentialFactory, same pattern as GmailApiClient)
- [ ] **Step 3: Register in DI** (AddGmailReader extension method)
- [ ] **Step 4: Run tests — pass**
- [ ] **Step 5: Commit**

### Task C2: AgentEmailFetchWorker

**Files:**
- Create: `apps/api/RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.EmailFetch/AgentEmailFetchWorker.cs`
- Create: corresponding test project + tests

- [ ] **Step 1: Write tests** (fetches 100 sent + 100 inbox, extracts signature, strips noise)
- [ ] **Step 2: Implement** (calls IGmailReader, parses email signatures, strips quoted replies/signatures from corpus)
- [ ] **Step 3: Run tests — pass**
- [ ] **Step 4: Commit**

### Task C3: AgentDriveIndexWorker

**Files:**
- Create: `apps/api/RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.DriveIndex/AgentDriveIndexWorker.cs`
- Create: corresponding tests

- [ ] **Step 1: Write tests** (lists files, filters real estate docs, finds/creates real-estate-star folder, reads content)
- [ ] **Step 2: Implement** (calls IGDriveClient, categorizes docs by type, reads full content)
- [ ] **Step 3: Run tests — pass**
- [ ] **Step 4: Commit**

### Task C4: AgentDiscoveryWorker

**Files:**
- Create: `apps/api/RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.AgentDiscovery/AgentDiscoveryWorker.cs`
- Create: corresponding tests

- [ ] **Step 1: Write tests** (downloads headshot, downloads logo, extracts phone, fetches websites, fetches 3rd party profiles with reviews + listings, extracts GA4 keys, checks WhatsApp)
- [ ] **Step 2: Implement** (HttpClient for websites, IOAuthRefresher for Google UserInfo photo, IWhatsAppClient for status check)
- [ ] **Step 3: Run tests — pass**
- [ ] **Step 4: Commit**

---

## Stream D: Phase 2 Synthesis Workers — Batch 1 (after Stream A)

Each synthesis worker follows the same pattern:
1. Accept gathered context (emails, Drive docs, websites, etc.)
2. Sanitize input via IContentSanitizer
3. Build structured Claude prompt with `<user-data>` tags
4. Call IAnthropicClient
5. Validate output structure
6. Return markdown

### Task D1: ContentSanitizer Implementation

**Files:**
- Create: `apps/api/RealEstateStar.DataServices/Shared/ContentSanitizer.cs`
- Create: corresponding tests

- [ ] **Step 1: Write tests** (strips HTML, removes Unicode tricks, removes injection patterns, truncates long content)
- [ ] **Step 2: Implement IContentSanitizer**
- [ ] **Step 3: Run tests — pass**
- [ ] **Step 4: Commit**

### Task D2: VoiceExtractionWorker

**Files:**
- Create: `apps/api/RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.VoiceExtraction/VoiceExtractionWorker.cs`
- Create: corresponding tests

- [ ] **Step 1: Write tests** (produces valid Voice Skill markdown, includes core directive + templates, handles low-data gracefully)
- [ ] **Step 2: Implement** (sanitize → build prompt with email corpus + Drive docs → call Claude → validate output → return markdown)
- [ ] **Step 3: Run tests — pass**
- [ ] **Step 4: Commit**

### Task D3: PersonalityWorker

**Files:** Same pattern as D2

- [ ] **Step 1-4: TDD cycle** — produces Personality Skill with core identity, temperament, EQ, communication energy, relationship style

### Task D4: BrandingDiscoveryWorker

**Files:** Same pattern

- [ ] **Step 1-4: TDD cycle** — extracts colors from CSS, fonts from @font-face/Google Fonts, logo variants, template recommendation based on sold listing patterns + market positioning

### Task D5: CoachingWorker

**Files:** Same pattern

- [ ] **Step 1-4: TDD cycle** — produces Coaching Report with quick wins, lead nurturing gaps, CTA strength, fee coaching section, follow-up cadence, Real Estate Star feature recommendations

---

## Stream E: Phase 2 Synthesis Workers — Batch 2 (after Stream A)

### Task E1: CmaStyleWorker

- [ ] **Step 1-4: TDD cycle** — analyzes CMA docs from Drive, extracts layout/data/copy preferences. Skips if no CMAs found.

### Task E2: MarketingStyleWorker

- [ ] **Step 1-4: TDD cycle** — identifies marketing emails, extracts campaign types/design patterns/marketing voice. Returns brand-relevant signals for merge.

### Task E3: WebsiteStyleWorker

- [ ] **Step 1-4: TDD cycle** — analyzes scraped HTML for layout, content structure, lead capture patterns. Skips if no websites found.

### Task E4: PipelineAnalysisWorker

- [ ] **Step 1-4: TDD cycle** — maps active deals, stages, velocity, key relationships from inbox + sent corpus.

### Task E5: BrandExtractionWorker + BrandVoiceWorker

- [ ] **Step 1-4: TDD cycle** — both consume emails + Drive + brokerage website HTML. Brand extraction produces raw brand signals, BrandVoice produces raw voice signals. Neither persists — returns raw data for merge.

### Task E6: ComplianceAnalysisWorker

- [ ] **Step 1-4: TDD cycle** — extracts legal language from email disclaimers, website legal pages, Drive docs. Cross-references against packages/legal/ standards. Produces delta report.

### Task E7: FeeStructureWorker

- [ ] **Step 1-4: TDD cycle** — extracts commission rates, splits, negotiation patterns. Stored but not wired into communications.

---

## Stream F: Activities + Services (after Stream A)

### Task F1: AgentConfigService

**Files:**
- Create: `apps/api/RealEstateStar.Services/Activation/RealEstateStar.Services.AgentConfig/AgentConfigService.cs`
- Create: corresponding tests

- [ ] **Step 1: Write tests**
  - Single agent case (accountId == agentId) → generates account.json + content.json
  - Brokerage first agent → bootstraps account.json + agent config.json + both content.json
  - Brokerage subsequent agent → skips brokerage bootstrap, creates agent config.json + content.json
  - Validates against agent.schema.json
  - Does not overwrite existing config

- [ ] **Step 2: Implement** (detects single vs brokerage, generates config files from gathered data with field-by-field mapping per spec)
- [ ] **Step 3: Run tests — pass**
- [ ] **Step 4: Commit**

### Task F2: PersistAgentProfileActivity

**Files:**
- Create: `apps/api/RealEstateStar.Activities/Activation/RealEstateStar.Activities.Activation.PersistAgentProfile/PersistAgentProfileActivity.cs`
- Create: corresponding tests

- [ ] **Step 1: Write tests** (writes all per-agent files via IFileStorageProvider, calls AgentConfigService, saves headshot + logo, writes consent log)
- [ ] **Step 2: Implement** (fan-out write all activation outputs to real-estate-star/{agentId}/)
- [ ] **Step 3: Run tests — pass**
- [ ] **Step 4: Commit**

### Task F3: BrandMergeService

**Files:**
- Create: `apps/api/RealEstateStar.Services/Activation/RealEstateStar.Services.BrandMerge/BrandMergeService.cs`
- Create: corresponding tests

- [ ] **Step 1: Write tests** (creates new brand profile, merges with existing, produces enriched Brand Profile + Brand Voice)
- [ ] **Step 2: Implement** (reads existing brand files, sends to Claude for merge, returns enriched markdown)
- [ ] **Step 3: Run tests — pass**
- [ ] **Step 4: Commit**

### Task F4: BrandMergeActivity

**Files:**
- Create: `apps/api/RealEstateStar.Activities/Activation/RealEstateStar.Activities.Activation.BrandMerge/BrandMergeActivity.cs`
- Create: corresponding tests

- [ ] **Step 1: Write tests** (calls BrandMergeService, writes Brand Profile + Brand Voice to real-estate-star/{accountId}/)
- [ ] **Step 2: Implement**
- [ ] **Step 3: Run tests — pass**
- [ ] **Step 4: Commit**

### Task F5: WelcomeNotificationService

**Files:**
- Create: `apps/api/RealEstateStar.Services/Activation/RealEstateStar.Services.WelcomeNotification/WelcomeNotificationService.cs`
- Create: corresponding tests

- [ ] **Step 1: Write tests** (sends via WhatsApp if enabled, falls back to email, uses agent catchphrases, includes coaching tip + pipeline insight + site URL, idempotent)
- [ ] **Step 2: Implement** (draft with Voice + Personality + Brand Voice + Coaching, try WhatsApp then Gmail, track sent flag)
- [ ] **Step 3: Run tests — pass**
- [ ] **Step 4: Commit**

### Task F6: AgentContextLoader (DataServices)

**Files:**
- Create: `apps/api/RealEstateStar.DataServices/Activation/AgentContextLoader.cs`
- Create: corresponding tests

- [ ] **Step 1: Write tests** (loads all files, returns null if not activated, partial load for missing optional files, caching within pipeline run)
- [ ] **Step 2: Implement** (reads from IFileStorageProvider, populates AgentContext record)
- [ ] **Step 3: Register as IAgentContextLoader in DI**
- [ ] **Step 4: Run tests — pass**
- [ ] **Step 5: Commit**

---

## Stream G: Orchestrator (after Streams C, D, E, F)

### Task G1: ActivationOrchestrator — Skip-If-Complete + Phase 1

**Files:**
- Create: `apps/api/RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.Orchestrator/ActivationOrchestrator.cs`
- Create: corresponding tests

- [ ] **Step 1: Write tests**
  - Reads from Channel\<ActivationRequest\>
  - Skip-if-complete: all files present → skips pipeline, logs [ACTV-002]
  - Skip-if-complete: missing file → full re-run
  - Phase 1: dispatches 3 gather workers in parallel
  - Phase 1: saves checkpoint after completion

- [ ] **Step 2: Implement BackgroundService** with skip check + Phase 1 dispatch
- [ ] **Step 3: Run tests — pass**
- [ ] **Step 4: Commit**

### Task G2: ActivationOrchestrator — Phase 2 + Checkpoints

- [ ] **Step 1: Write tests**
  - Dispatches all synthesis workers in parallel
  - Checkpoint resume: skips completed workers
  - Handles worker failures (retry with checkpoint)
  - Tracks per-worker status

- [ ] **Step 2: Implement Phase 2 dispatch + checkpoint logic**
- [ ] **Step 3: Run tests — pass**
- [ ] **Step 4: Commit**

### Task G3: ActivationOrchestrator — Phase 3 + 4

- [ ] **Step 1: Write tests**
  - Calls PersistAgentProfileActivity with all gathered outputs
  - Calls BrandMergeActivity with brand signals
  - Calls WelcomeNotificationService
  - Cleans up checkpoints on completion

- [ ] **Step 2: Implement Phase 3 (Activities) + Phase 4 (Service) calls**
- [ ] **Step 3: Run tests — pass**
- [ ] **Step 4: Commit**

### Task G4: Register Orchestrator as HostedService

- [ ] **Step 1: Add to Program.cs**

```csharp
builder.Services.AddHostedService<ActivationOrchestrator>();
```

- [ ] **Step 2: Register all workers, activities, services in DI**
- [ ] **Step 3: Build + run architecture tests**
- [ ] **Step 4: Commit**

---

## Stream H: Lead Pipeline Integration (after Stream A — can run in parallel with B-G)

### Task H1: Inject AgentContext into LeadOrchestrator

**Files:**
- Modify: `apps/api/RealEstateStar.Workers/Leads/RealEstateStar.Workers.Lead.Orchestrator/LeadOrchestrator.cs`

- [ ] **Step 1: Write test** (LeadOrchestrator loads AgentContext at pipeline start)
- [ ] **Step 2: Add IAgentContextLoader as constructor dependency**
- [ ] **Step 3: Call LoadAsync at pipeline start, pass context downstream**
- [ ] **Step 4: Run tests — pass**
- [ ] **Step 5: Commit**

### Task H2: Update LeadEmailDrafter with Voice + Personality + Brand Voice + Coaching

**Files:**
- Modify: `apps/api/RealEstateStar.Workers/Leads/RealEstateStar.Workers.Lead.Orchestrator/LeadEmailDrafter.cs`

- [ ] **Step 1: Write tests** (drafts with skills injected, falls back to generic when no context)
- [ ] **Step 2: Modify prompt to include Voice Skill + Personality + Brand Voice + Branding Kit + Coaching sections**
- [ ] **Step 3: Run tests — pass**
- [ ] **Step 4: Commit**

### Task H3: Update CmaProcessingWorker + CmaPdfGenerator

**Files:**
- Modify: `apps/api/RealEstateStar.Workers/Leads/RealEstateStar.Workers.Lead.CMA/CmaProcessingWorker.cs`
- Modify: `apps/api/RealEstateStar.Workers/Leads/RealEstateStar.Workers.Lead.CMA/CmaPdfGenerator.cs`

- [ ] **Step 1: Write tests** (CMA uses style guide + brand voice, PDF uses branding kit colors/fonts/logo)
- [ ] **Step 2: Inject AgentContext, apply CMA Style Guide + Brand Voice + Branding Kit**
- [ ] **Step 3: Run tests — pass**
- [ ] **Step 4: Commit**

### Task H4: Update AgentNotifierService + WhatsApp ConversationHandler

**Files:**
- Modify: `apps/api/RealEstateStar.Services/RealEstateStar.Services.AgentNotifier/AgentNotifierService.cs`
- Modify: `apps/api/RealEstateStar.Workers/WhatsApp/RealEstateStar.Workers.WhatsApp/ConversationHandler.cs`

- [ ] **Step 1: Write tests** (notifications use Voice + Brand Voice, WhatsApp uses casual register)
- [ ] **Step 2: Inject IAgentContextLoader, apply context per spec's email type table**
- [ ] **Step 3: Run tests — pass**
- [ ] **Step 4: Commit**

---

## Stream I: Integration Tests + Final Verification

### Task I1: End-to-End OAuth → Activation Flow

- [ ] **Step 1: Write integration test** — generate link → hit authorize → mock Google exchange → verify token stored → verify ActivationRequest enqueued
- [ ] **Step 2: Run test — pass**
- [ ] **Step 3: Commit**

### Task I2: End-to-End Activation Pipeline

- [ ] **Step 1: Write integration test** — enqueue ActivationRequest → verify all outputs written → verify account.json generated → verify Brand Profile created
- [ ] **Step 2: Run test — pass**
- [ ] **Step 3: Commit**

### Task I3: Full Solution Build + Architecture Tests

- [ ] **Step 1: Build entire solution**

```bash
dotnet build apps/api/RealEstateStar.Api.sln
```

- [ ] **Step 2: Run ALL tests**

```bash
dotnet test apps/api/RealEstateStar.Api.sln
```

- [ ] **Step 3: Run coverage**

```bash
bash apps/api/scripts/coverage.sh
```

- [ ] **Step 4: Verify architecture tests pass**

```bash
dotnet test apps/api/tests/RealEstateStar.Architecture.Tests/
```

- [ ] **Step 5: Commit + push**

---

## Execution Timeline (Wednesday Demo Target)

| Day | Streams | What gets done |
|-----|---------|----------------|
| **Sat night** | A | Foundation: Domain models, interfaces, project scaffolding, config |
| **Sun** | B + C + D + E + F in parallel | OAuth endpoints, all workers, activities, services |
| **Mon** | G + H in parallel | Orchestrator, lead pipeline integration |
| **Tue** | I | Integration tests, full build verification, demo prep |
| **Wed** | — | Demo |

**Parallel agent allocation:**
- Agent 1: Stream B (OAuth endpoints)
- Agent 2: Stream C (gather workers)
- Agent 3: Stream D (synthesis workers batch 1)
- Agent 4: Stream E (synthesis workers batch 2)
- Agent 5: Stream F (activities + services)
- Agent 6: Stream H (lead pipeline integration)
- Agent 7: Stream G (orchestrator — starts after C/D/E/F have interfaces)
- Agent 8: Stream I (integration tests)
