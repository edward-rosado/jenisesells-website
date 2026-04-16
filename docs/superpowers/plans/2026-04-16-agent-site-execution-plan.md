# Implementation Plan: Agent Site Comprehensive Design

## Overview

End-to-end system for generating, previewing, publishing, and multi-hosting agent websites from activation pipeline data. The implementation spans 18 work streams across 4 waves, with 12 streams starting day-1 in parallel. The critical path is S1 (Cloudflare client) -> S12b (PersistSiteContent) -> S14 (Preview state machine) -> S18 (Migration tooling), totaling approximately 12 engineering days.

The plan is built on R6 simplifications: reuse of existing `IDistributedContentCache`, `AccountConfigService`, `ClaudeDiagnostics`; sealed `VoicedContentGenerator` (no interface); one `ActivationDiagnostics` class; pure-Markdown legal templates; SLO thresholds deferred to post-baseline.

## Requirements

### Functional
- G1: Generate publishable agent site from activation outputs alone (no hand-crafted content)
- G2: Support single-agent and brokerage topologies simultaneously
- G3: Multi-lingual sites with per-locale resynthesis in agent's voice
- G4: Capture prospective agent data from brokerage scrapes
- G5: BYOD custom domains (agent reachable at up to 3 URLs)
- G6: Gate billing behind preview + approval
- G7: Legal pages generated from templates per-locale with lawyer disclaimer
- G8: Stay within Y1 Consumption memory envelope
- G9: Preserve existing PR preview build flow
- G10: Per-activation Claude cost <= $1.20 (monolingual) / $1.60 (bilingual)
- G11: Legally compliant (Fair Housing, TCPA, ADA/WCAG 2.1 AA, state advertising)

### R6 Constraints (Inviolable)
- No `IVoicedContentGenerator` interface -- sealed class injected directly
- Reuse existing `IDistributedContentCache` + `TableStorageContentCache`
- Reuse existing `ClaudeDiagnostics` -- add `pipeline_step` tag, no new cost counter
- One `ActivationDiagnostics` class with bounded `component` tag
- `EtagCasRetryPolicy.ExecuteAsync` shared static helper
- `SsrfGuard` and `HtmlTextExtractor` are sealed classes, NOT interfaces
- `IRoutingPolicyStore` and `IFairHousingLinter` keep interfaces
- Legal pages: pure Markdown templates, zero Claude calls
- SLO thresholds: `TBD_POST_BASELINE`
- Preview session: 5 fields only
- Section 12 (Refresh Worker) scope is DELETED

## Architecture Changes

### New Projects (Backend .NET)
- `apps/api/RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.SiteFactExtractor/` -- new worker project
- `apps/api/RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.TeamScrape/` -- new worker project

### New Files (Backend .NET)
- `apps/api/RealEstateStar.Domain/Activation/SiteFacts.cs` -- SiteFacts DTO and all child records
- `apps/api/RealEstateStar.Domain/Activation/VoicedRequest.cs` -- VoicedRequest<T>, VoicedBatchRequest<T>, VoicedResult<T>
- `apps/api/RealEstateStar.Domain/Activation/FieldSpec.cs` -- FieldSpec<T> record
- `apps/api/RealEstateStar.Domain/Activation/FieldSpecs/*.cs` -- 15 declarative field spec files
- `apps/api/RealEstateStar.Domain/Activation/PreviewSession.cs` -- 5-field PreviewSession record
- `apps/api/RealEstateStar.Domain/Activation/RoutingPolicy.cs` -- routing-policy.json schema DTO
- `apps/api/RealEstateStar.Domain/Activation/BrokerageRoutingConsumption.cs` -- Azure Table row DTO
- `apps/api/RealEstateStar.Domain/Activation/ProspectiveAgent.cs` -- prospect subsystem DTO
- `apps/api/RealEstateStar.Domain/Activation/BuildResult.cs` -- Full/Fallback/NeedsInfo result type
- `apps/api/RealEstateStar.Clients/RealEstateStar.Clients.Anthropic/VoicedContentGenerator.cs` -- sealed class
- `apps/api/RealEstateStar.Clients/RealEstateStar.Clients.Cloudflare/CloudflareKvClient.cs` -- KV CRUD
- `apps/api/RealEstateStar.Clients/RealEstateStar.Clients.Cloudflare/CloudflareR2Client.cs` -- R2 put/get/delete
- `apps/api/RealEstateStar.Clients/RealEstateStar.Clients.Cloudflare/CloudflareForSaasClient.cs` -- custom hostname provisioning
- `apps/api/RealEstateStar.Clients/RealEstateStar.Clients.Stripe/StripeWebhookHandler.cs` -- signature + idempotency
- `apps/api/RealEstateStar.Clients/RealEstateStar.Clients.Stripe/StripeCheckoutService.cs` -- checkout session creation
- `apps/api/RealEstateStar.Clients/RealEstateStar.Clients.Azure/BrokerageRoutingConsumptionStore.cs` -- Azure Table row CRUD
- `apps/api/RealEstateStar.Clients/RealEstateStar.Clients.Azure/CustomHostnamesStore.cs` -- Azure Table hostname lifecycle
- `apps/api/RealEstateStar.Clients/RealEstateStar.Clients.Azure/PreviewSessionStore.cs` -- Azure Table session CRUD
- `apps/api/RealEstateStar.Clients/RealEstateStar.Clients.Azure/StripeEventStore.cs` -- event ID idempotency table
- `apps/api/RealEstateStar.Workers.Shared/Concurrency/EtagCasRetryPolicy.cs` -- shared CAS retry helper
- `apps/api/RealEstateStar.Workers.Shared/Security/SsrfGuard.cs` -- sealed SSRF validation
- `apps/api/RealEstateStar.Workers.Shared/Security/HtmlTextExtractor.cs` -- sealed static XSS sanitizer
- `apps/api/RealEstateStar.DataServices/Diagnostics/ActivationDiagnostics.cs` -- consolidated diagnostics
- `apps/api/RealEstateStar.DataServices/Legal/LegalPageRenderer.cs` -- sealed static template renderer
- `apps/api/RealEstateStar.Functions/Diagnostics/DurableOrchestratorTracingMiddleware.cs` -- DF trace middleware
- `apps/api/RealEstateStar.Functions/Diagnostics/TelemetryRegistrations.cs` -- single-file source of truth
- `apps/api/RealEstateStar.Functions/Activation/Activities/BuildLocalizedSiteContentFunction.cs`
- `apps/api/RealEstateStar.Functions/Activation/Activities/PersistSiteContentFunction.cs`
- `apps/api/RealEstateStar.Functions/Activation/Activities/RehostAssetsToR2Function.cs`
- `apps/api/RealEstateStar.Functions/Activation/Activities/BrokerageJoinFunction.cs`
- `apps/api/RealEstateStar.Functions/Bvd/DomainVerificationFunction.cs` -- background hostname verification
- `apps/api/RealEstateStar.Api/Features/Domains/SubmitDomain/SubmitDomainEndpoint.cs`
- `apps/api/RealEstateStar.Api/Features/Domains/VerifyDomain/VerifyDomainEndpoint.cs`
- `apps/api/RealEstateStar.Api/Features/Domains/DeleteDomain/DeleteDomainEndpoint.cs`
- `apps/api/RealEstateStar.Api/Features/Domains/ListDomains/ListDomainsEndpoint.cs`
- `apps/api/RealEstateStar.Api/Features/Preview/ExchangeToken/ExchangeTokenEndpoint.cs`
- `apps/api/RealEstateStar.Api/Features/Preview/RevokeSession/RevokeSessionEndpoint.cs`
- `apps/api/RealEstateStar.Api/Features/Sites/Approve/ApproveEndpoint.cs`
- `apps/api/RealEstateStar.Api/Features/Sites/Publish/PublishEndpoint.cs` -- Stripe webhook
- `apps/api/RealEstateStar.Api/Features/Sites/GetState/GetStateEndpoint.cs`

### Modified Files (Backend .NET)
- `apps/api/RealEstateStar.Domain/Shared/Interfaces/Storage/IAccountConfigService.cs` -- add `SaveIfUnchangedAsync`
- `apps/api/RealEstateStar.Domain/Shared/Models/AccountConfig.cs` -- add `ETag` property
- `apps/api/RealEstateStar.DataServices/Config/AccountConfigService.cs` -- implement `SaveIfUnchangedAsync`
- `apps/api/RealEstateStar.Clients/RealEstateStar.Clients.Anthropic/ClaudeDiagnostics.cs` -- add `pipeline_step` tag to `RecordUsage`
- `apps/api/RealEstateStar.Clients/RealEstateStar.Clients.Azure/TableStorageContentCache.cs` -- add cache hit/miss counters
- `apps/api/RealEstateStar.Api/Diagnostics/OpenTelemetryExtensions.cs` -- import `TelemetryRegistrations.All`, register DF middleware
- `apps/api/RealEstateStar.Functions/Activation/ActivationOrchestratorFunction.cs` -- wire Phase 2.75 + 3 activities (S17 ONLY)
- `apps/api/RealEstateStar.Functions/Activation/ActivityNames.cs` -- add new activity name constants
- `apps/api/RealEstateStar.Functions/Activation/ActivationRetryPolicies.cs` -- add SiteContent retry policy

### New Files (Frontend)
- `apps/agent-site/features/config/hybrid-loader.ts` -- env-aware KV vs bundled fixture loading

### Modified Files (Frontend)
- `apps/agent-site/features/templates/*.tsx` -- 10 files, extract `defaultContent` exports
- `apps/agent-site/middleware.ts` -- custom domain resolution + preview cookie handling

### New Config/Infra Files
- `config/legal/templates/_defaults/en/privacy.md`
- `config/legal/templates/_defaults/en/terms.md`
- `config/legal/templates/_defaults/en/accessibility.md`
- `config/legal/templates/_defaults/en/fair-housing.md`
- `config/legal/templates/_defaults/en/tcpa.md`
- `config/legal/templates/_defaults/es/privacy.md` (+ terms, accessibility, fair-housing)
- `config/legal/templates/_defaults/pt/privacy.md` (+ terms, accessibility, fair-housing)
- `config/legal/templates/by-state/NJ/en/fair-housing.md`
- `config/legal/templates/by-state/CA/en/privacy.md`
- `config/legal/templates/by-state/NY/en/accessibility.md`
- `infra/grafana/dashboards/activation-pipeline.json`
- `infra/grafana/dashboards/brokerage-routing.json`
- `infra/grafana/dashboards/support-triage.json`
- `infra/grafana/alerts/*.yaml` -- 14 alert rule files
- `scripts/migrate-account.ps1`
- `docs/runbooks/migrate-existing-accounts.md`
- `docs/runbooks/trace-reconstruction.md`

## Implementation Steps

### PR-0: DTO Foundation (Prerequisite, blocks all waves)

**Purpose**: Ship all shared DTOs in a single PR so every subsequent PR can reference them without merge conflicts. This is the only thing that blocks Wave 1.

1. **Create SiteFacts DTO hierarchy** (File: `apps/api/RealEstateStar.Domain/Activation/SiteFacts.cs`)
   - Action: Define `SiteFacts`, `AgentIdentity`, `BrokerageIdentity`, `LocationFacts`, `SpecialtiesFacts`, `TrustSignals`, `RecentSale`, `Review`, `Credential`, `PipelineStages`, `LocaleVoice` as sealed records per spec SS5.3
   - Why: Every worker and activity in subsequent PRs depends on this type graph
   - Dependencies: None
   - Risk: Low -- pure data records, no logic

2. **Create VoicedRequest/Result/FieldSpec generics** (File: `apps/api/RealEstateStar.Domain/Activation/VoicedRequest.cs`, `FieldSpec.cs`)
   - Action: Define `VoicedRequest<T>`, `VoicedBatchRequest<T>`, `VoicedResult<T>`, `FieldSpec<T>`, `ClaudeCallMetrics` per spec SS5.3
   - Why: S3b (VoicedContentGenerator) and S3c (FieldSpec catalog) both consume these types
   - Dependencies: SiteFacts (step 1)
   - Risk: Low

3. **Create PreviewSession record** (File: `apps/api/RealEstateStar.Domain/Activation/PreviewSession.cs`)
   - Action: Define 5-field `PreviewSession(SessionId, AccountId, ExpiresAt, Revoked, RevokedAt)` per R6-12
   - Why: S14 (Preview state machine) depends on this type
   - Dependencies: None
   - Risk: Low

4. **Create RoutingPolicy and BrokerageRoutingConsumption DTOs** (File: `apps/api/RealEstateStar.Domain/Activation/RoutingPolicy.cs`, `BrokerageRoutingConsumption.cs`)
   - Action: Define `RoutingPolicy` (mirrors routing-policy.json schema), `BrokerageRoutingConsumption` (Azure Table row with PolicyContentHash, Counter, OverrideConsumed, ETag)
   - Why: S9a and S9b depend on these
   - Dependencies: None
   - Risk: Low

5. **Create ProspectiveAgent record** (File: `apps/api/RealEstateStar.Domain/Activation/ProspectiveAgent.cs`)
   - Action: Define fields per spec SS7: Name, Title, Email, Phone, BioSnippet, HeadshotUrl, Specialties, ServiceAreas, ScrapedAt, LastRefreshedAt, SourceUrl, AccountId
   - Why: S7b (TeamScrapeWorker) depends on this
   - Dependencies: None
   - Risk: Low

6. **Create BuildResult type** (File: `apps/api/RealEstateStar.Domain/Activation/BuildResult.cs`)
   - Action: Define `BuildResult(ResultType, IReadOnlyDictionary<string, ContentConfig> ContentByLocale, string? FallbackReason)` with `ResultType` enum `{Full, Fallback, NeedsInfo}` per spec SS13.4
   - Why: S12a activity and S17 orchestrator wire-up depend on this
   - Dependencies: None
   - Risk: Low

7. **Create CasAttemptResult and CasOutcome value types** (File: `apps/api/RealEstateStar.Domain/Shared/Models/CasTypes.cs`)
   - Action: Define `CasAttemptResult(Committed, ShouldRetry, Reason)` and `CasOutcome(Succeeded, AttemptCount, FailureReason)` as readonly record structs per spec SS8.3
   - Why: S4 (EtagCasRetryPolicy) and its consumers depend on these
   - Dependencies: None
   - Risk: Low

8. **Add ETag property to AccountConfig** (File: `apps/api/RealEstateStar.Domain/Shared/Models/AccountConfig.cs`)
   - Action: Add `string? ETag` property to the existing `AccountConfig` record
   - Why: S8a (SaveIfUnchangedAsync) needs this for optimistic concurrency
   - Dependencies: None
   - Risk: Low -- additive change to existing record

9. **Add SaveIfUnchangedAsync to IAccountConfigService** (File: `apps/api/RealEstateStar.Domain/Shared/Interfaces/Storage/IAccountConfigService.cs`)
   - Action: Add `Task<bool> SaveIfUnchangedAsync(AccountConfig account, string etag, CancellationToken ct)` to existing interface
   - Why: S8a, S8b depend on this signature being available
   - Dependencies: AccountConfig ETag property (step 8)
   - Risk: Low -- additive interface change, implementation follows in S8a

**PR-0 Estimated size**: ~200 lines of pure records and interface additions across 8-9 files. All in `RealEstateStar.Domain/`. Zero logic, zero dependencies.

---

### Phase A: Wave 1 -- Foundations (Days 1-3, 12 parallel PRs)

Every PR in this phase has zero dependencies on any other Wave 1 PR. All reuse existing codebase primitives already on `main` / `feat/azure-cost-reduction`.

#### A1: S0 -- Observability Foundation (Agent: Backend-Observability)

1. **Create TelemetryRegistrations** (File: `apps/api/RealEstateStar.Functions/Diagnostics/TelemetryRegistrations.cs`)
   - Action: Create static class with `All` property containing every `ActivitySource` name and `Meter` name in the codebase (22 existing + new `RealEstateStar.Activation`). Both `Api/Program.cs` and `Functions/Program.cs` import this single list.
   - Why: Single source of truth prevents drift between API and Functions hosts (SSE.3 rule 5)
   - Dependencies: PR-0 merged
   - Risk: Low -- inventory existing 22 sources from `OpenTelemetryExtensions.cs`

2. **Create ActivationDiagnostics** (File: `apps/api/RealEstateStar.DataServices/Diagnostics/ActivationDiagnostics.cs`)
   - Action: Create static class with `ServiceName = "RealEstateStar.Activation"`, counters (`Operations`, `Conflicts`, `RetriesExhausted`, `VoicedFallbacks`), histogram (`Duration`), bounded `component` tag enum. Pattern follows existing `TokenStoreDiagnostics.cs`.
   - Why: Consolidated observability for account_config, routing, voiced_content, persist components
   - Dependencies: PR-0 merged
   - Risk: Low

3. **Create 5-6 architecture tests** (Files: `apps/api/RealEstateStar.Tests/RealEstateStar.Architecture.Tests/`)
   - Action: Add tests: `LegalPages_UseTemplates`, `Caches_EmitHitMissMetrics`, `Orchestrators_UseReplaySafeLogger`, `MetricTags_AreBoundedCardinality`, `ActivityInputs_HaveCorrelationId`, `TelemetryRegistrationIsCentralized`
   - Why: Every subsequent PR is enforced by these arch tests from day 1
   - Dependencies: PR-0 merged
   - Risk: Medium -- must correctly enumerate all sources; verify `IsReplaying` discipline

4. **Update OpenTelemetryExtensions** (File: `apps/api/RealEstateStar.Api/Diagnostics/OpenTelemetryExtensions.cs`)
   - Action: Replace 21 individual `AddSource(...)` calls with a loop over `TelemetryRegistrations.All`. Same for `AddMeter(...)` calls.
   - Why: Centralizes telemetry registration, enables arch test enforcement
   - Dependencies: Step 1 (TelemetryRegistrations)
   - Risk: Medium -- must verify all 22 existing sources still registered

#### A2: S1 -- Cloudflare Client (Agent: Backend-Infrastructure)

1. **Implement CloudflareKvClient** (File: `apps/api/RealEstateStar.Clients/RealEstateStar.Clients.Cloudflare/CloudflareKvClient.cs`)
   - Action: Create class implementing `ICloudflareKvClient` (define interface in Domain). Methods: `GetAsync<T>(namespaceId, key)`, `PutAsync<T>(namespaceId, key, value, metadata?)`, `DeleteAsync(namespaceId, key)`, `ListAsync(namespaceId, prefix?)`. Uses Cloudflare REST API v4 at `https://api.cloudflare.com/client/v4/accounts/{accountId}/storage/kv/namespaces/{namespaceId}/values/{key}`.
   - Why: Foundation for content persistence (S12b), preview state (S14), hybrid loader (S11)
   - Dependencies: PR-0 merged; existing empty `Clients.Cloudflare.csproj`
   - Risk: Medium -- first Cloudflare REST integration; test with WireMock stubs

2. **Implement CloudflareR2Client** (File: `apps/api/RealEstateStar.Clients/RealEstateStar.Clients.Cloudflare/CloudflareR2Client.cs`)
   - Action: Create class for S3-compatible R2 API. Methods: `PutObjectAsync(bucket, key, stream, contentType)`, `GetObjectAsync(bucket, key)`, `DeleteObjectAsync(bucket, key)`. Uses S3-compatible endpoint at `https://{accountId}.r2.cloudflarestorage.com`.
   - Why: Asset rehosting in S12c
   - Dependencies: None
   - Risk: Medium -- S3 signing required (use `AWSSDK.S3` or manual V4 signing)

3. **Implement CloudflareForSaasClient** (File: `apps/api/RealEstateStar.Clients/RealEstateStar.Clients.Cloudflare/CloudflareForSaasClient.cs`)
   - Action: Create class wrapping Cloudflare for SaaS API. Methods: `CreateCustomHostnameAsync(hostname)`, `DeleteCustomHostnameAsync(hostnameId)`, `GetCustomHostnameAsync(hostnameId)`. Uses `https://api.cloudflare.com/client/v4/zones/{zoneId}/custom_hostnames`.
   - Why: BYOD domain provisioning in S10b/S10c
   - Dependencies: None
   - Risk: Medium -- Cloudflare for SaaS setup must be pre-configured in CF dashboard

4. **Write tests** (File: `apps/api/RealEstateStar.Tests/RealEstateStar.Clients.Cloudflare.Tests/`)
   - Action: WireMock-based tests for all three clients: happy path, 404, 429 rate limit, auth failure, malformed response
   - Why: Integration with third-party REST API needs thorough fault simulation
   - Dependencies: Steps 1-3
   - Risk: Low

**Estimated total**: ~300-400 lines of C# across 3 client files + interface definitions in Domain

#### A3: S2 -- defaultContent Refactor (Agent: Frontend)

1. **Extract hardcoded titles to defaultContent exports** (Files: 10 files in `apps/agent-site/features/templates/*.tsx`)
   - Action: Each template file (emerald-classic, luxury-estate, warm-community, modern-minimal, coastal-living, commercial, country-estate, light-luxury, new-beginnings, urban-loft) exports a `defaultContent` object with default titles for every section it renders. Section components resolve `content.{section}.data.title || template.defaultContent.{section}.title`.
   - Why: Eliminates hardcoded strings in JSX, enables pipeline-generated content to override every field
   - Dependencies: PR-0 merged
   - Risk: Low -- pure refactor, no behavior change

2. **Add validation test** (File: `apps/agent-site/__tests__/config/defaultContent.test.ts`)
   - Action: Test that every section component used in at least one template has a corresponding `defaultContent` entry. If a template references `<ServicesGrid>` but its `defaultContent.features` is missing, test fails.
   - Why: Prevents silent regressions when templates are edited
   - Dependencies: Step 1
   - Risk: Low

#### A4: S3a -- SiteFactExtractor (Agent: Backend-Pipeline-A)

1. **Create SiteFactExtractor worker project** (Dir: `apps/api/RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.SiteFactExtractor/`)
   - Action: New csproj referencing Domain + Workers.Shared. Main class `SiteFactExtractorWorker` takes `ActivationOutputs` (VoiceSkill, PersonalitySkill, AgentDiscovery, BrandingKit, PipelineJson, transactions, profile scrapes) and produces `SiteFacts`.
   - Why: Locale-neutral fact substrate consumed by every localization branch
   - Dependencies: PR-0 (SiteFacts DTO)
   - Risk: Medium -- must correctly map 16 markdown skill files to SiteFacts fields

2. **Implement fact extraction logic**
   - Action: Pure compute: parse ActivationOutputs, extract agent identity from AgentDiscovery, brokerage identity from BrandingKit, location facts from profile scrapes, specialties from skills, trust signals from transactions, recent sales, testimonials, credentials. Hash all facts for cache key.
   - Why: Deterministic, same inputs produce same outputs, cacheable for replay
   - Dependencies: Step 1
   - Risk: Medium -- mapping coverage of all fields

3. **Write 12+ golden-output tests** (File: `apps/api/RealEstateStar.Tests/RealEstateStar.Workers.Tests/RealEstateStar.Workers.Activation.SiteFactExtractor.Tests/`)
   - Action: Use canned ActivationOutputs fixtures. Assert every SiteFacts field is populated correctly. Test with missing optional fields (no credentials, no testimonials). Test FactsHash determinism.
   - Why: Golden-output tests catch extraction regressions
   - Dependencies: Steps 1-2
   - Risk: Low

#### A5: S3b -- VoicedContentGenerator (Agent: Backend-Pipeline-B)

1. **Implement VoicedContentGenerator sealed class** (File: `apps/api/RealEstateStar.Clients/RealEstateStar.Clients.Anthropic/VoicedContentGenerator.cs`)
   - Action: Create sealed class injecting `IAnthropicClient`, `IDistributedContentCache` (existing), `IFairHousingLinter`, `ILogger<VoicedContentGenerator>`. Implement `GenerateAsync<T>` and `GenerateBatchAsync<T>` per spec SS5.3. Responsibilities: prompt assembly, locale voice injection, schema validation, retry (2 retries on 429/5xx), cost logging via existing `ClaudeDiagnostics.RecordUsage`, fair housing linting, content-hash caching with 24h TTL.
   - Why: Single abstraction for all T3 field generation, eliminates 15 duplicate Claude call sites
   - Dependencies: PR-0 (VoicedRequest<T>, FieldSpec<T>, VoicedResult<T> DTOs); existing `IDistributedContentCache` at `apps/api/RealEstateStar.Domain/Shared/Interfaces/IDistributedContentCache.cs`; existing `ClaudeDiagnostics` at `apps/api/RealEstateStar.Clients/RealEstateStar.Clients.Anthropic/ClaudeDiagnostics.cs`
   - Risk: High -- core abstraction; prompt quality directly affects site quality

2. **Add pipeline_step tag to ClaudeDiagnostics** (File: `apps/api/RealEstateStar.Clients/RealEstateStar.Clients.Anthropic/ClaudeDiagnostics.cs`)
   - Action: Extend `RecordUsage` signature to accept optional `string? pipelineStep` parameter. Add `pipeline_step` tag to all counter Tag lists when provided. Backward compatible -- existing callers pass null.
   - Why: Cost attribution per field (hero, tagline, features, etc.) in Grafana
   - Dependencies: None (existing file)
   - Risk: Low -- additive signature change

3. **Write unit tests with canned Claude responses** (File: `apps/api/RealEstateStar.Tests/RealEstateStar.Clients.Anthropic.Tests/VoicedContentGeneratorTests.cs`)
   - Action: Test cache hit path, cache miss with successful generation, schema validation failure -> fallback, fair housing violation -> one rewrite -> clean, fair housing violation -> two failures -> fallback, Claude error after retries -> fallback, batch generation. Mock `IAnthropicClient`, `IDistributedContentCache`, `IFairHousingLinter`.
   - Why: Generator is the core abstraction; every failure path must be verified
   - Dependencies: Steps 1-2
   - Risk: Low

#### A6: S3c -- FieldSpec Catalog (Agent: Backend-Pipeline-C)

1. **Create 15 declarative field specs** (Dir: `apps/api/RealEstateStar.Domain/Activation/FieldSpecs/`)
   - Action: Create one file per field spec: `HeroHeadline.cs`, `HeroTagline.cs`, `HeroCtaText.cs`, `AboutBio.cs`, `AboutSubtitle.cs`, `FeaturesTitle.cs`, `FeaturesItems.cs`, `StepsTitle.cs`, `StepsItems.cs`, `TestimonialsTitle.cs`, `ContactTitle.cs`, `ContactSubtitle.cs`, `ThankYouHeadline.cs`, `ThankYouMessage.cs`, `NavLabels.cs`. Each defines `Name`, `PromptTemplate`, `MaxOutputTokens`, `Model` (haiku-4-5 or sonnet-4-6), `Schema` validator, `FallbackValue`.
   - Why: Pure data; declarative spec for what to generate per field
   - Dependencies: PR-0 (FieldSpec<T>); depends on S3b signature only (not implementation)
   - Risk: Low -- pure data files, no logic

2. **Write validation tests** (File: `apps/api/RealEstateStar.Tests/RealEstateStar.Domain.Tests/FieldSpecValidationTests.cs`)
   - Action: Assert every field spec has a non-empty PromptTemplate, positive MaxOutputTokens, valid Model string, non-null FallbackValue. Assert all spec Names are unique.
   - Why: Catch misconfigured specs before they reach production
   - Dependencies: Step 1
   - Risk: Low

#### A7: S4 -- EtagCasRetryPolicy (Agent: Backend-Shared)

1. **Implement EtagCasRetryPolicy** (File: `apps/api/RealEstateStar.Workers/RealEstateStar.Workers.Shared/Concurrency/EtagCasRetryPolicy.cs`)
   - Action: Create static class with `ExecuteAsync(maxAttempts, attemptFn, logger, component, ct)` per spec SS8.3. Exponential backoff with jitter (50ms * 2^attempt, capped at 2s, +0-50ms jitter). Records conflicts and exhaustion via `ActivationDiagnostics` counters.
   - Why: Shared retry primitive for SS8.3 (account merge) and SS8.4 (routing CAS)
   - Dependencies: PR-0 (CasAttemptResult, CasOutcome)
   - Risk: Low -- simple static method

2. **Write tests** (File: `apps/api/RealEstateStar.Tests/RealEstateStar.Workers.Tests/RealEstateStar.Workers.Shared.Tests/EtagCasRetryPolicyTests.cs`)
   - Action: Test: success on first attempt, success on 3rd attempt after 2 conflicts, exhaustion after max attempts, ShouldRetry=false early exit, cancellation token respected, backoff timing verification
   - Dependencies: Step 1
   - Risk: Low

#### A8: S5 -- Locale Registry (Agent: Backend-I18n)

1. **Generalize LanguageDetector** (File: existing `apps/api/RealEstateStar.Domain/Shared/Services/LanguageDetector.cs`)
   - Action: Refactor to support a registry of known locales with detection thresholds. Add Portuguese detection alongside existing English/Spanish. Ensure `DetectLocale(text)` returns BCP 47 codes.
   - Why: Pipeline must detect Portuguese (and future locales) from agent communications
   - Dependencies: PR-0 merged
   - Risk: Low -- extending existing class

2. **Generalize VoiceExtractionWorker locale handling** (File: existing `apps/api/RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.VoiceExtraction/`)
   - Action: Make locale iteration registry-driven instead of hardcoded en/es. Worker iterates over `agent.Languages` from config and processes each detected locale.
   - Why: When Portuguese content is added (S6), the worker automatically handles it
   - Dependencies: Step 1
   - Risk: Medium -- must not break existing en/es tests

#### A9: S7a -- Security Utilities (Agent: Backend-Security)

1. **Implement SsrfGuard** (File: `apps/api/RealEstateStar.Workers/RealEstateStar.Workers.Shared/Security/SsrfGuard.cs`)
   - Action: Sealed class with `SafeGetAsync(url, maxBodyBytes, timeout, maxRedirects, ct)`. Enforces: HTTPS-only scheme, private IP rejection (RFC 1918, loopback, link-local, CGNAT, IPv6 equivalents), infrastructure hostname blocklist (`.real-estate-star.com`, `.internal`, `.local`, `.azurewebsites.net`), DNS rebinding defense via `SocketsHttpHandler.ConnectCallback`, redirect revalidation (max 3), body size cap, gzip decompression ratio cap (10x), 15s total timeout, identifiable User-Agent. Per spec SS16.4.
   - Why: Every outbound scraping HTTP call must go through SSRF validation
   - Dependencies: PR-0 merged
   - Risk: High -- security-critical; the 10 checks from SS16.4 must all be present

2. **Implement HtmlTextExtractor** (File: `apps/api/RealEstateStar.Workers/RealEstateStar.Workers.Shared/Security/HtmlTextExtractor.cs`)
   - Action: Sealed static class with `ToPlainText(string html)` using AngleSharp. Strips all tags, attributes, scripts, inline handlers. Returns visible text only.
   - Why: XSS sanitization on every scraped string before persistence
   - Dependencies: None
   - Risk: Medium -- must handle malformed HTML gracefully

3. **Write test matrix** (File: `apps/api/RealEstateStar.Tests/RealEstateStar.Workers.Tests/RealEstateStar.Workers.Shared.Tests/Security/`)
   - Action: SsrfGuard tests per spec matrix: `http://example.com` rejected, `https://169.254.169.254/latest/meta-data/` rejected, private IPs rejected, DNS rebind rejected, redirect to private IP rejected, 6MB body truncated, gzip bomb aborted, normal 200 OK success. HtmlTextExtractor tests: `<script>alert(1)</script>` stripped, `<img src=x onerror=alert(1)>` stripped, `javascript:alert(1)` stripped, normal HTML preserved as text.
   - Dependencies: Steps 1-2
   - Risk: Low

#### A10: S9a -- Routing Schema (Agent: Backend-Routing)

1. **Implement BrokerageRoutingConsumptionStore** (File: `apps/api/RealEstateStar.Clients/RealEstateStar.Clients.Azure/BrokerageRoutingConsumptionStore.cs`)
   - Action: Azure Table CRUD for `brokerage-routing-consumption` table. PK = accountId, RK = "consumption". Fields: `PolicyContentHash`, `Counter`, `OverrideConsumed`, `LastDecisionAt`, ETag for CAS. Methods: `GetAsync`, `SaveIfUnchangedAsync` (same pattern as `AzureTableTokenStore`).
   - Why: Atomic consumption record for Option C lead routing
   - Dependencies: PR-0 (BrokerageRoutingConsumption DTO)
   - Risk: Low -- follows existing `AzureTableTokenStore` pattern exactly

2. **Add IRoutingPolicyStore interface** (File: `apps/api/RealEstateStar.Domain/Shared/Interfaces/Storage/IRoutingPolicyStore.cs`)
   - Action: Define `Task<RoutingPolicy?> GetPolicyAsync(string accountId, CancellationToken ct)`. This crosses a real storage boundary (Drive read), so it keeps an interface per R6-4.
   - Why: S9b routing algorithm needs to read the Drive policy file
   - Dependencies: PR-0 (RoutingPolicy DTO)
   - Risk: Low

3. **Write tests** (File: `apps/api/RealEstateStar.Tests/RealEstateStar.Clients.Azure.Tests/BrokerageRoutingConsumptionStoreTests.cs`)
   - Action: Test CAS success, CAS conflict (412), read after write, hash mismatch triggers reset
   - Dependencies: Steps 1-2
   - Risk: Low

#### A11: S10a -- DF Tracing Middleware (Agent: Backend-Observability)

1. **Implement DurableOrchestratorTracingMiddleware** (File: `apps/api/RealEstateStar.Functions/Diagnostics/DurableOrchestratorTracingMiddleware.cs`)
   - Action: Implement `IFunctionsWorkerMiddleware`. On orchestrator invocations: extract instance ID, create parent span with stable trace ID derived from instance ID (SHA-256 hash, truncated to 16 bytes for W3C trace ID), propagate correlation ID via `System.Diagnostics.Baggage`. On activity invocations: extract correlation from Baggage, add as span attribute. Per spec SS5.8.
   - Why: Today 18-activity activation produces 18 disconnected spans; after this, one connected trace
   - Dependencies: A1 (TelemetryRegistrations for registration path)
   - Risk: High -- DF replay semantics mean the middleware fires multiple times; must not create duplicate spans on replay

2. **Register middleware from OpenTelemetryExtensions** (File: `apps/api/RealEstateStar.Functions/Program.cs` or equivalent)
   - Action: Add `.UseMiddleware<DurableOrchestratorTracingMiddleware>()` to the Functions worker builder
   - Why: Single registration point per R6-6
   - Dependencies: Step 1
   - Risk: Low

3. **Write tests** (File: `apps/api/RealEstateStar.Tests/RealEstateStar.Functions.Tests/Diagnostics/`)
   - Action: Test trace ID determinism (same instance ID -> same trace ID), correlation propagation via Baggage, replay safety (second invocation with same context does not create new span)
   - Dependencies: Step 1
   - Risk: Medium

#### A12: S11-init -- Hybrid Loader Scaffold (Agent: Frontend)

1. **Create hybrid-loader stub** (File: `apps/agent-site/features/config/hybrid-loader.ts`)
   - Action: Export `loadContent(handle, locale, env)` function. When `env === 'preview'`, return from bundled `config-registry.ts`. When `env === 'production'`, stub the KV fetch path (wiring to real KV comes in Wave 2 S11). Include TypeScript types for the loader result.
   - Why: Establishes the loader contract; frontend and backend can develop independently
   - Dependencies: PR-0 merged
   - Risk: Low -- stub with clear contract

2. **Write tests** (File: `apps/agent-site/__tests__/config/hybrid-loader.test.ts`)
   - Action: Test preview env returns bundled fixture, production env calls KV stub, missing handle returns null, locale fallback to English
   - Dependencies: Step 1
   - Risk: Low

---

### Phase B: Wave 2 -- Pipeline Core (Days 4-7, 11 parallel PRs)

Each PR has exactly 1-2 Wave 1 dependencies.

#### B1: S6 -- Portuguese Content (Agent: Frontend-I18n)

1. **Add Portuguese content fixture** (File: `config/accounts/test-modern/content-pt.json`)
   - Action: Create Portuguese content file for the test-modern fixture. Follow existing `content-es.json` pattern.
   - Why: Third locale for testing the full pipeline
   - Dependencies: A8 (locale registry supports pt)
   - Risk: Low

#### B2: S7b -- TeamScrapeWorker (Agent: Backend-Scraping)

1. **Create TeamScrapeWorker project** (Dir: `apps/api/RealEstateStar.Workers/Activation/RealEstateStar.Workers.Activation.TeamScrape/`)
   - Action: New csproj. Worker takes brokerage URL, fetches team page via `SsrfGuard.SafeGetAsync`, parses HTML via `HtmlTextExtractor.ToPlainText`, extracts `ProspectiveAgent` records (name, title, email, phone, bio, headshot URL, specialties, service areas). Respects robots.txt, uses identifiable User-Agent, caps response at 5MB.
   - Why: Captures prospective agents from brokerage scrape for warm-start activations (G4)
   - Dependencies: A9 (SsrfGuard + HtmlTextExtractor)
   - Risk: High -- external HTML varies wildly; extraction heuristics need many fixture tests

2. **Write tests with canned HTML** (File: `apps/api/RealEstateStar.Tests/RealEstateStar.Workers.Tests/RealEstateStar.Workers.Activation.TeamScrape.Tests/`)
   - Action: 10+ fixtures of real brokerage team pages (anonymized). Test extraction accuracy, robots.txt compliance, SSRF rejection, XSS sanitization on all fields.
   - Dependencies: Step 1
   - Risk: Medium -- fixture creation requires real-world page sampling

#### B3: S8a -- SaveIfUnchangedAsync (Agent: Backend-Config)

1. **Implement SaveIfUnchangedAsync in AccountConfigService** (File: `apps/api/RealEstateStar.DataServices/Config/AccountConfigService.cs`)
   - Action: Add `SaveIfUnchangedAsync(AccountConfig account, string etag, CancellationToken ct)` method. Serialize config to JSON, write to temp file, atomic rename. For Azure Table backing (production path): use `UpdateEntity` with ETag precondition, return `false` on 412, `true` on success. Populate `ETag` on every `GetAccountAsync` read.
   - Why: Enables optimistic concurrency for brokerage-join (SS8.3)
   - Dependencies: A7 (EtagCasRetryPolicy available); PR-0 (interface already has signature)
   - Risk: Medium -- must handle both file-based (dev) and Azure Table (prod) paths

2. **Write tests** (File: `apps/api/RealEstateStar.Tests/RealEstateStar.DataServices.Tests/Config/AccountConfigServiceConcurrencyTests.cs`)
   - Action: Two concurrent saves with same ETag: one succeeds, one returns false. ETag changes after successful save. Null ETag on fresh creation.
   - Dependencies: Step 1
   - Risk: Low

#### B4: S8b -- BrokerageJoinActivity (Agent: Backend-Brokerage)

1. **Implement BrokerageJoinFunction** (File: `apps/api/RealEstateStar.Functions/Activation/Activities/BrokerageJoinFunction.cs`)
   - Action: Activity function that reads account via `IAccountConfigService.GetAccountAsync`, checks idempotency (agent already in list -> no-op), appends new agent, calls `SaveIfUnchangedAsync` via `EtagCasRetryPolicy.ExecuteAsync` with 5 max attempts. Unique log codes `[ACCOUNT-MERGE-001..040]`.
   - Why: Atomic brokerage join with concurrency safety
   - Dependencies: B3 (SaveIfUnchangedAsync), A7 (EtagCasRetryPolicy)
   - Risk: Medium -- concurrent join scenario needs integration test

2. **Write tests including concurrency integration test**
   - Action: Unit test: success on first attempt, success after 2 conflicts, exhaustion after 5, idempotent replay. Integration test: 10 parallel joins, all succeed, final account has all 10 agents.
   - Dependencies: Step 1
   - Risk: Medium

#### B5: S9b -- Routing Algorithm (Agent: Backend-Routing)

1. **Implement RoutingService** (File: `apps/api/RealEstateStar.DataServices/Routing/RoutingService.cs`)
   - Action: Implements the Option C routing algorithm: (1) read `routing-policy.json` from Drive via `IRoutingPolicyStore`, (2) compute SHA-256 content hash, (3) read Azure Table consumption row, (4) if hash differs -> reset Counter and OverrideConsumed, (5) if `next_lead` set and not consumed -> route to named agent via CAS commit, (6) else weighted round-robin with `EtagCasRetryPolicy.ExecuteAsync`, (7) append to `routing-log.csv` in Drive as audit trail.
   - Why: Intelligent lead routing preserving Drive-as-human-editable-policy
   - Dependencies: A10 (routing schema), A7 (EtagCasRetryPolicy)
   - Risk: High -- most complex algorithm in this feature set; race conditions need thorough testing

2. **Write comprehensive test matrix** (File: `apps/api/RealEstateStar.Tests/RealEstateStar.DataServices.Tests/Routing/`)
   - Action: Test: override consumption (CAS success), override consumption (CAS conflict -> fallback to round-robin), round-robin across 3 agents, disabled agent skipped, hash change resets counter, concurrent routing decisions, empty policy, single-agent policy
   - Dependencies: Step 1
   - Risk: Medium

#### B6: S10b -- POST /domains + Hostnames Table (Agent: Backend-Domains)

1. **Implement CustomHostnamesStore** (File: `apps/api/RealEstateStar.Clients/RealEstateStar.Clients.Azure/CustomHostnamesStore.cs`)
   - Action: Azure Table CRUD for `real-estate-star-custom-hostnames`. PK = normalized domain, RK = "hostname". Full lifecycle fields per spec SS9.2.
   - Why: Persistent storage for domain lifecycle state
   - Dependencies: A2 (CloudflareForSaasClient)
   - Risk: Low

2. **Implement domain endpoints** (Files: `apps/api/RealEstateStar.Api/Features/Domains/`)
   - Action: Create `SubmitDomainEndpoint` (POST /domains), `VerifyDomainEndpoint` (POST /domains/{hostname}/verify), `DeleteDomainEndpoint` (DELETE /domains/{hostname}), `ListDomainsEndpoint` (GET /domains). Include hostname validation: RFC 1123 format, not IP, not subdomain of real-estate-star.com, TLD blocklist, label blocklist with Levenshtein distance, IDN homoglyph check. Rate limiting: 10/min, 100/day per account. Auth: JWT bearer per SS16.3 matrix.
   - Why: Full BYOD domain management API
   - Dependencies: Step 1, A2 (CloudflareForSaasClient)
   - Risk: Medium -- hostname validation is non-trivial; Levenshtein + IDN checks

3. **Write tests** (File: `apps/api/RealEstateStar.Tests/RealEstateStar.Api.Tests/Features/Domains/`)
   - Action: Test each endpoint: valid hostname, invalid hostname (IP, .gov TLD, typosquat), rate limiting, auth failure, CNAME verification success/failure, Cloudflare provisioning error
   - Dependencies: Steps 1-2
   - Risk: Low

#### B7: S10c -- DNS Verification Job (Agent: Backend-Domains)

1. **Implement DomainVerificationFunction** (File: `apps/api/RealEstateStar.Functions/Bvd/DomainVerificationFunction.cs`)
   - Action: Timer-triggered function (every 5 minutes). For each pending hostname: resolve TXT `_realstar-challenge.{hostname}` from 3 public resolvers (1.1.1.1, 8.8.8.8, 9.9.9.9), require consensus. For verified hostnames: resolve CNAME, if points to `real-estate-star.com` -> call `CloudflareForSaasClient.CreateCustomHostnameAsync`. For live hostnames (daily re-verification): resolve routing record, if fails consecutively -> suspend after 2 failures, remove after 7 days.
   - Why: Two-phase DNS verification with takeover prevention per SS9.5, SS16.2
   - Dependencies: B6 (CustomHostnamesStore), A2 (CloudflareForSaasClient)
   - Risk: High -- DNS resolution is inherently unreliable; multi-resolver consensus adds complexity

2. **Write tests** (File: `apps/api/RealEstateStar.Tests/RealEstateStar.Functions.Tests/Bvd/`)
   - Action: Mock DNS resolution. Test: TXT challenge matches -> ownership verified, TXT mismatch -> stays pending, CNAME verified -> provisions SSL, re-verification failure -> suspend, 7-day suspension -> remove, DNS timeout handled gracefully
   - Dependencies: Step 1
   - Risk: Medium

#### B8: S11 -- Hybrid Loader Complete (Agent: Frontend)

1. **Wire hybrid loader to real KV** (File: `apps/agent-site/features/config/hybrid-loader.ts`)
   - Action: Replace the KV stub from A12 with actual Cloudflare Workers KV binding. Use `env.CONTENT_KV.get(key)` for production reads. Implement fallback chain: KV hit -> return; KV miss -> try bundled fixtures; both miss -> 404.
   - Why: Completes the production content loading path
   - Dependencies: A2 (CloudflareKvClient establishes KV key schema), A12 (hybrid loader scaffold)
   - Risk: Medium -- Cloudflare Workers KV binding requires wrangler config updates

2. **Write integration tests** (File: `apps/agent-site/__tests__/config/hybrid-loader-kv.test.ts`)
   - Action: Test with mocked KV binding: production env reads from KV, fallback to bundled, draft vs live key selection based on preview cookie
   - Dependencies: Step 1
   - Risk: Low

#### B9: S12a -- BuildLocalizedSiteContentActivity (Agent: Backend-Pipeline-Core)

1. **Implement BuildLocalizedSiteContentFunction** (File: `apps/api/RealEstateStar.Functions/Activation/Activities/BuildLocalizedSiteContentFunction.cs`)
   - Action: Activity function that orchestrates: (1) check feature flags, (2) run SiteFactExtractor, (3) select template via deterministic score function, (4) for each supported locale in parallel (max 2 concurrent): invoke VoicedContentGenerator for all T3 fields via batched calls, copy T1 fields from SiteFacts, derive D fields per rules, (5) render legal pages via LegalPageRenderer, (6) validate content against nav-section invariant. Returns `BuildResult`. Implements tier-2 fallback per SS13.4: if fact extraction fails or all locales fail, generates minimal content from account.json + template defaultContent.
   - Why: The central activity that produces per-locale content.json files
   - Dependencies: A4 (SiteFactExtractor), A5 (VoicedContentGenerator), A6 (FieldSpec catalog)
   - Risk: High -- most complex activity; many moving parts; template selection algorithm

2. **Implement template selection score function** (File: same or adjacent helper)
   - Action: Pure C# deterministic scoring: color brightness match, vibe match, luxury weight, commercial weight, warmth weight. Returns argmax over 10 templates.
   - Why: Template choice is not a Claude call -- pure compute from SiteFacts
   - Dependencies: SiteFacts DTO
   - Risk: Medium -- scoring weights need tuning via test fixtures

3. **Write tests** (File: `apps/api/RealEstateStar.Tests/RealEstateStar.Functions.Tests/Activation/Activities/BuildLocalizedSiteContentFunctionTests.cs`)
   - Action: Test: full generation success, tier-1 retry on locale failure, tier-2 fallback on fact extraction failure, tier-2 fallback on all locales failing, feature flag disabled, account denylist, fallback still passes compliance checks, nav-section invariant validated
   - Dependencies: Steps 1-2
   - Risk: Medium

#### B10: S12b -- PersistSiteContentActivity (Agent: Backend-Persist)

1. **Implement PersistSiteContentFunction** (File: `apps/api/RealEstateStar.Functions/Activation/Activities/PersistSiteContentFunction.cs`)
   - Action: Activity function that persists BuildResult to: (a) Cloudflare KV as draft (`content:{accountId}:{locale}:draft`), (b) Azure Blob backup, (c) agent Drive folder (markdown representations). Also writes `account.json` to KV (`account:{accountId}`), legal pages to KV (`legal:{accountId}:{page}:{locale}:draft`), and updates `site-state:{accountId}` to `pending_approval`. Compensation on partial failure: if KV write succeeds but blob backup fails, log warning but don't fail the activity (KV is the source of truth).
   - Why: Writes content to all persistence targets for production serving
   - Dependencies: A2 (CloudflareKvClient, CloudflareR2Client), B9 DTO (BuildResult)
   - Risk: Medium -- multi-target write with compensation logic

2. **Write tests** (File: `apps/api/RealEstateStar.Tests/RealEstateStar.Functions.Tests/Activation/Activities/PersistSiteContentFunctionTests.cs`)
   - Action: Test: successful multi-target write, KV failure -> activity fails (retry), blob failure -> log warning + continue, idempotent replay, correct key format per locale
   - Dependencies: Step 1
   - Risk: Low

#### B11: S12c -- RehostAssetsToR2Activity (Agent: Backend-Assets)

1. **Implement RehostAssetsToR2Function** (File: `apps/api/RealEstateStar.Functions/Activation/Activities/RehostAssetsToR2Function.cs`)
   - Action: Activity function that downloads HeadshotBytes, BrokerageLogoBytes, BrokerageIconBytes from activation outputs, uploads to R2 at `agents/{accountId}/{agentId}/{asset}.{ext}`, rewrites asset URLs in account.json to `https://assets.real-estate-star.com/agents/{accountId}/{agentId}/{asset}.{ext}`. Lazy: only processes assets that exist. Idempotent: uses content-hash paths or ETag-conditional uploads.
   - Why: Assets served from Cloudflare R2 (zero egress, CDN-backed) instead of external brokerage URLs
   - Dependencies: A2 (CloudflareR2Client), B9 DTO (knows which assets exist)
   - Risk: Medium -- binary download + upload needs size caps (max 2 concurrent per memory budget)

2. **Write tests**
   - Action: Test: headshot rehost success, missing headshot skipped, idempotent re-upload, concurrent cap respected
   - Dependencies: Step 1
   - Risk: Low

---

### Phase C: Wave 3 -- Preview + Ship (Days 8-11, 4 parallel PRs)

#### C1: S13 -- LegalPageRenderer + Templates (Agent: Backend-Legal)

1. **Create legal page template files** (Dir: `config/legal/templates/`)
   - Action: Create the full template tree per spec SS11.2: `_defaults/en/{privacy,terms,accessibility,fair-housing,tcpa}.md`, `_defaults/es/{privacy,terms,accessibility,fair-housing}.md` (no tcpa.es.md -- TCPA is English only), `_defaults/pt/{same as es}`, `by-state/NJ/en/fair-housing.md`, `by-state/CA/en/privacy.md`, `by-state/NY/en/accessibility.md`. Each template uses `{{brokerage.name}}`, `{{agent.name}}`, `{{agent.state}}`, `{{agent.state_full}}`, `{{generated_at}}` variables.
   - Why: Pure Markdown templates reviewed by lawyers, zero LLM involvement per R6-8
   - Dependencies: B9 DTO (SiteFacts provides template variables)
   - Risk: Low -- static files

2. **Implement LegalPageRenderer** (File: `apps/api/RealEstateStar.DataServices/Legal/LegalPageRenderer.cs`)
   - Action: Sealed static class with `Render(templatePath, variables)` using `string.Replace` for each `{{variable}}`. State resolution order: by-state/{state}/{locale}/{page}.md -> _defaults/{locale}/{page}.md -> _defaults/en/{page}.md. Appends legal disclaimer footer per SS11.4. Called from `BuildLocalizedSiteContentActivity`.
   - Why: Deterministic, auditable legal page generation
   - Dependencies: Step 1
   - Risk: Low -- simple string.Replace, no external dependencies

3. **Write tests** (File: `apps/api/RealEstateStar.Tests/RealEstateStar.DataServices.Tests/Legal/LegalPageRendererTests.cs`)
   - Action: Test: default template renders with variables substituted, NJ state override used when state=NJ, English fallback when locale not found, disclaimer footer present in every output, TCPA stays English regardless of locale, all 5 pages render for en/es/pt
   - Dependencies: Steps 1-2
   - Risk: Low

4. **Verify arch test** -- `LegalPages_UseTemplates` (from A1) should now pass: no `IAnthropicClient` injection in the legal page path.

#### C2: S14 -- Preview State Machine (Agent: Backend-Core)

1. **Implement PreviewSessionStore** (File: `apps/api/RealEstateStar.Clients/RealEstateStar.Clients.Azure/PreviewSessionStore.cs`)
   - Action: Azure Table CRUD for `real-estate-star-preview-sessions`. PK = sessionId. 5 fields per R6-12: SessionId, AccountId, ExpiresAt, Revoked, RevokedAt. Methods: `CreateAsync`, `GetAsync`, `RevokeAsync`, `RefreshExpiryAsync` (sliding 24h window, hard cap 30d).
   - Why: Server-side session storage for preview authentication
   - Dependencies: PR-0 (PreviewSession DTO)
   - Risk: Low

2. **Implement ExchangeTokenEndpoint** (File: `apps/api/RealEstateStar.Api/Features/Preview/ExchangeToken/ExchangeTokenEndpoint.cs`)
   - Action: POST /preview-sessions/exchange. Validates HMAC-signed exchange token (15-min TTL, single-use), creates session row, returns session ID. Rate limited 10/min per IP.
   - Why: One-time exchange from email link to opaque session cookie
   - Dependencies: Step 1
   - Risk: Medium -- HMAC validation, single-use token consumption

3. **Implement RevokeSessionEndpoint** (File: `apps/api/RealEstateStar.Api/Features/Preview/RevokeSession/RevokeSessionEndpoint.cs`)
   - Action: DELETE /preview-sessions/{sessionId}. Auth: agent session OR valid exchange token. Sets Revoked=true, RevokedAt=now.
   - Why: "I didn't request this" flow and admin revocation
   - Dependencies: Step 1
   - Risk: Low

4. **Implement ApproveEndpoint** (File: `apps/api/RealEstateStar.Api/Features/Sites/Approve/ApproveEndpoint.cs`)
   - Action: POST /sites/{accountId}/approve. Auth: preview session cookie OR agent JWT. Validates session scope (accountId match), updates site-state to `pending_billing`, creates Stripe checkout session with server-side `STRIPE_PRICE_ID` and idempotency key `approve:{accountId}:{previewSessionId}`. Returns `{ checkoutUrl }`. Rate limited 5/min per account.
   - Why: Gates billing behind agent approval; Stripe checkout session creation
   - Dependencies: Step 1, B10 (content persisted to KV)
   - Risk: High -- Stripe integration, idempotency key correctness

5. **Implement GetStateEndpoint** (File: `apps/api/RealEstateStar.Api/Features/Sites/GetState/GetStateEndpoint.cs`)
   - Action: GET /sites/{accountId}/state. Auth: agent JWT. Returns current `site-state:{accountId}` from KV.
   - Why: Dashboard and admin visibility into site lifecycle
   - Dependencies: Step 1
   - Risk: Low

6. **Write comprehensive tests** (Files: `apps/api/RealEstateStar.Tests/RealEstateStar.Api.Tests/Features/Preview/`, `Features/Sites/`)
   - Action: Test every endpoint per SS16.3 auth matrix: valid auth, missing auth, wrong account, expired token, revoked session, double exchange (second attempt fails), duplicate approve (idempotent checkout), state transitions. Test sliding window: use session, verify ExpiresAt extended, verify hard cap respected.
   - Dependencies: Steps 1-5
   - Risk: Medium

#### C3: S15 -- Worker Routing (Agent: Frontend-Advanced)

1. **Update agent-site middleware** (File: `apps/agent-site/middleware.ts`)
   - Action: Add custom domain resolution: on request, check hostname against `routing:{hostname}` KV key; if found, extract `accountId` and `agentId`. Add preview cookie handling: if `rs_preview` cookie present, validate session via KV/API, serve draft content instead of live. Handle exchange token redirect: if `?x=` parameter present, POST to `/preview-sessions/exchange`, set cookie, 302 redirect to strip `?x=`.
   - Why: Custom domain and preview flow integration at the Worker layer
   - Dependencies: B7 (S10c -- DNS verification complete means hostnames exist in KV), C2 (S14 -- preview session API available)
   - Risk: High -- middleware is the critical path for every request; must not break production traffic

2. **Write tests** (File: `apps/agent-site/__tests__/middleware.test.ts`)
   - Action: Test: platform subdomain routing, custom domain routing, preview cookie + draft content serving, exchange token redirect, expired session rejection, cross-tenant session rejection (accountId mismatch -> 403)
   - Dependencies: Step 1
   - Risk: Medium

#### C4: S16 -- Stripe Webhook Integrity (Agent: Backend-Billing)

1. **Implement StripeWebhookHandler** (File: `apps/api/RealEstateStar.Clients/RealEstateStar.Clients.Stripe/StripeWebhookHandler.cs`)
   - Action: Wraps `Stripe.Webhook.ConstructEvent` with 300s tolerance. Event ID idempotency via `StripeEventStore` (Azure Table with 30-day TTL). Stripe IP allowlist enforcement.
   - Why: Webhook security per SS16.5
   - Dependencies: C2 (approve endpoint creates the checkout session that triggers the webhook)
   - Risk: High -- security-critical; signature verification is primary defense

2. **Implement StripeEventStore** (File: `apps/api/RealEstateStar.Clients/RealEstateStar.Clients.Azure/StripeEventStore.cs`)
   - Action: Azure Table `real-estate-star-stripe-events`. PK = event ID, TTL = 30 days. Insert-only for idempotency.
   - Why: Prevent duplicate webhook processing
   - Dependencies: None
   - Risk: Low

3. **Implement PublishEndpoint** (File: `apps/api/RealEstateStar.Api/Features/Sites/Publish/PublishEndpoint.cs`)
   - Action: POST /sites/{accountId}/publish. Stripe webhook handler only. Validates signature, IP allowlist, event ID idempotency, payment_status="paid", metadata.accountId match. On success: copy `content:{accountId}:{locale}:draft` to `content:{accountId}:{locale}:live` for each locale, update `site-state:{accountId}` to `live`, purge edge cache, enqueue "Your site is live!" email.
   - Why: The only code path that promotes draft to live -- gated behind Stripe payment
   - Dependencies: Steps 1-2, C2 (site state exists)
   - Risk: High -- must correctly handle all failure modes per SS16.5

4. **Write tests per SS16.5 matrix** (File: `apps/api/RealEstateStar.Tests/RealEstateStar.Api.Tests/Features/Sites/Publish/`)
   - Action: Valid signature -> processed, invalid signature -> 400, missing signature -> 400, duplicate event ID -> 200 no-op, accountId mismatch -> 400, non-Stripe IP -> 403, unpaid status -> 200 no-op
   - Dependencies: Steps 1-3
   - Risk: Low

---

### Phase D: Wave 4 -- Wire-up + Migration (Days 12-15, 2 serialized PRs)

#### D1: S17 -- Orchestrator Dispatch (Agent: Backend-Orchestrator)

1. **Wire new activities into ActivationOrchestratorFunction** (File: `apps/api/RealEstateStar.Functions/Activation/ActivationOrchestratorFunction.cs`)
   - Action: Add new activities **at the end** of the existing orchestrator (never insert between existing calls -- replay safety). Add Phase 2.75 (SiteFactExtractor + BuildLocalizedSiteContent per locale in parallel) and Phase 3 extensions (PersistSiteContent, RehostAssetsToR2, LegalPageRenderer invocations). Use `ActivationRetryPolicies` for all new `CallActivityAsync` calls. Add BrokerageJoinActivity to the join path.
   - Why: This is the ONLY PR that touches the orchestrator -- all other activities developed in isolation
   - Dependencies: B9, B10, B11, C1 (all activities and their tests must be merged)
   - Risk: High -- orchestrator replay safety is critical; MUST purge all running/failed instances before deploying

2. **Update ActivityNames** (File: `apps/api/RealEstateStar.Functions/Activation/ActivityNames.cs`)
   - Action: Add constants for all new activity names
   - Dependencies: Step 1
   - Risk: Low

3. **Update ActivationRetryPolicies** (File: `apps/api/RealEstateStar.Functions/Activation/ActivationRetryPolicies.cs`)
   - Action: Add `SiteContent` retry policy (max 3 attempts, 30s backoff, 2x coefficient -- same pattern as existing Synthesis/Persist)
   - Dependencies: Step 1
   - Risk: Low

4. **Write orchestrator tests** (File: `apps/api/RealEstateStar.Tests/RealEstateStar.Workers.Tests/RealEstateStar.Workers.Activation.Orchestrator.Tests/`)
   - Action: Test: full pipeline runs through new phases, SiteFactExtractor failure -> tier-2 fallback, partial locale failure -> best-effort, PersistSiteContent failure -> activity retry, BrokerageJoinActivity concurrent scenario. Test replay safety with existing phase ordering preserved.
   - Dependencies: Steps 1-3
   - Risk: High

#### D2: S18 -- Migration Tooling + Runbook (Agent: DevOps)

1. **Create migration script** (File: `scripts/migrate-account.ps1`)
   - Action: Script that: (1) snapshots current `config/accounts/{handle}/` to backup, (2) enqueues ActivationRequest, (3) waits for completion (not polling -- single command, check manually per Eddie's preference), (4) provides diff command to compare old vs new content
   - Why: Controlled migration of existing production accounts
   - Dependencies: D1 (orchestrator dispatches all new activities)
   - Risk: Medium -- script must handle both dev (file) and prod (Azure Table + KV) paths

2. **Create migration runbook** (File: `docs/runbooks/migrate-existing-accounts.md`)
   - Action: Step-by-step guide per SS13.1: pre-flight backup, trigger activation, watch Grafana, compare output, spot-check preview, promote to live, cleanup after 30 days. Include rollback steps per SS13.3.
   - Why: Operational documentation for safe migration
   - Dependencies: Step 1
   - Risk: Low

3. **Create trace reconstruction runbook** (File: `docs/runbooks/trace-reconstruction.md`)
   - Action: Document how support reconstructs full pipeline trace from a single correlation ID. Include Grafana query examples, Tempo search patterns, audit record cross-references.
   - Why: Per SS16.10 -- support can paste a ref ID into Tempo instead of a 30-minute log dive
   - Dependencies: A11 (DF tracing middleware provides the connected traces)
   - Risk: Low

---

### Phase E: Production Cutover (Day 16+)

Not a PR phase -- manual operational work:

1. Manually re-trigger activation for `jenise-buckalew` under new pipeline
2. Verify outputs in Grafana, diff against current content
3. Spot-check preview at `jenise-buckalew.real-estate-star.com`
4. Promote to live via preview -> approve -> bill flow
5. Repeat for `safari-homes`, `glr`
6. Declare migration complete
7. Begin 14-day SLO baseline collection (fill in `TBD_POST_BASELINE` thresholds)

---

## Parallel Execution Matrix

Shows which agents can work simultaneously per wave. Agent = an engineer or AI coding agent.

```
                    Day 1   Day 2   Day 3   Day 4   Day 5   Day 6   Day 7   Day 8   Day 9   Day 10  Day 11  Day 12  Day 13  Day 14  Day 15
PR-0 (all)          ===
Agent-Observability  [------A1------]        [--B1--]                                
Agent-Infrastructure [----------A2----------] [----------B6----------] [----------B7----------]
Agent-Frontend-A     [------A3------]        [B1]   [----------B8----------]         [----------C3----------]
Agent-Pipeline-A     [----------A4----------] [------------------B9------------------]         [------D1------]
Agent-Pipeline-B     [----------A5----------] [----------B10----------]                        [------D1------]
Agent-Pipeline-C     [------A6------]        [------B11------]
Agent-Shared         [--A7--]                [--B3--] [----------B4----------]
Agent-I18n           [------A8------]        [------B2(start)---]
Agent-Security       [------A9------]        [--------------------B2(complete)---]
Agent-Routing        [------A10-----]        [----------B5----------]
Agent-DF-Tracing     [------A11-----]        (available for other work)
Agent-Frontend-B     [------A12-----]        (joins B8 or C3)
Agent-Legal                                                                          [----------C1----------]
Agent-Core                                                                           [------------------C2------------------]
Agent-Billing                                                                                             [------C4------]
Agent-DevOps                                                                                                              [------D2------]
```

**Peak parallelism**: 12 agents on Day 1-3, 11 agents on Day 4-7, 4-5 agents on Day 8-11, 2-3 agents on Day 12-15.

**Minimum viable staffing**: 3 engineers. Assign one to critical path (A2 -> B10 -> C2 -> D1), one to pipeline core (A4 -> A5 -> B9 -> C1), one to everything else. ~3 weeks total.

---

## Testing Strategy

### Unit Tests (every PR)
- Pure functions: template selection score, routing algorithm, fact extractor logic, EtagCasRetryPolicy fault injection
- SsrfGuard: all 10 SS16.4 checks
- HtmlTextExtractor: XSS injection vectors
- LegalPageRenderer: template variable substitution, state override resolution, disclaimer footer
- VoicedContentGenerator: cache hit/miss, fallback paths, fair housing linting
- FieldSpec catalog: validation of all 15 specs

### Component Tests (per worker/activity)
- SiteFactExtractor with canned ActivationOutputs
- VoicedContentGenerator with canned Claude responses
- TeamScrapeWorker with canned HTML fixtures
- BuildLocalizedSiteContent with mocked workers
- PersistSiteContent with mocked KV/R2/blob clients

### Activity Tests (per DF activity)
- Activity functions with mocked worker outputs
- Verify correct blobs/KV entries written
- Retry behavior on transient failures

### Orchestrator Tests (S17 only)
- Replay semantics: new activities added at end, existing order preserved
- Phase ordering verification
- Failure recovery: best-effort vs fatal activity classification

### Integration Tests
- End-to-end: enqueue ActivationRequest for test handle against test Cloudflare account
- Assert final KV + R2 state matches expected content
- 10 concurrent BrokerageJoinActivity invocations: all agents present in final account
- Stripe webhook: real signature verification with test mode keys

### Architecture Tests (A1, enforced from day 1)
- `LegalPages_UseTemplates` -- no IAnthropicClient in legal path
- `Caches_EmitHitMissMetrics` -- every cache has hit/miss counters
- `Orchestrators_UseReplaySafeLogger` -- `!ctx.IsReplaying` check on all log calls
- `MetricTags_AreBoundedCardinality` -- metric tags have O(10) cardinality
- `ActivityInputs_HaveCorrelationId` -- every activity input DTO has CorrelationId
- `TelemetryRegistrationIsCentralized` -- both hosts import same list
- `VoicedContentGenerator_IsOnlyCallerOfAnthropicForVoicedFields`
- `TeamScrapeWorker_CannotCallHttpClientDirectly`
- `StripeWebhook_VerifiesSignature`
- `PreviewToken_NotInQueryParam`

### Validation Tests
- Generated `content.json` passes nav-section invariant
- Portuguese content valid
- All legal pages present for every locale

### Smoke Tests (post-deploy)
- Hit preview URL for test agent, verify render
- Verify existing production accounts still serve correctly

---

## Risks and Mitigations

- **Risk**: 12+ PRs merging simultaneously cause merge conflicts
  - Mitigation: PR-0 ships all shared DTOs first. Each PR touches one project root. SS15.11 hygiene rules: no PR rewrites orchestrator until S17. CI branch protection requires arch tests to pass.

- **Risk**: Orchestrator replay safety broken by new activity dispatch order
  - Mitigation: S17 is the ONLY PR that touches `ActivationOrchestratorFunction.cs`. New activities added at END only. All running/failed instances purged before deploy.

- **Risk**: Claude cost exceeds $1.60/activation budget
  - Mitigation: `ClaudeDiagnostics.CostUsd` counter with `pipeline_step` tag in Grafana. Haiku-4-5 for most fields, Sonnet only for bio. 24h content-hash cache prevents duplicate calls on replay. Alert at $2.50/activation.

- **Risk**: Cloudflare KV/R2 API integration failures in production
  - Mitigation: A2 ships with WireMock-style tests. Integration tests against test Cloudflare account before merging S12b. R2 is S3-compatible -- well-documented API.

- **Risk**: SLO thresholds produce false alarms on first deploy
  - Mitigation: Thresholds are `TBD_POST_BASELINE`. Alert structure committed, numbers filled after 14-day production baseline.

- **Risk**: DNS verification false positives/negatives for BYOD domains
  - Mitigation: Multi-resolver consensus (3 resolvers must agree). Re-verification daily for live hostnames. 2-consecutive-failure threshold before suspension. 7-day grace before removal.

- **Risk**: Legal page templates miss state-specific requirements
  - Mitigation: by-state/ override system. Publish gate: site cannot go live with missing state required fields. NJ/CA/NY overrides seeded from day 1. Additional states added as agents sign up.

- **Risk**: Stripe webhook replayed by attacker
  - Mitigation: Signature verification (primary), IP allowlist (secondary), event ID idempotency (tertiary), metadata.accountId scope check (quaternary). Defense in depth.

- **Risk**: Preview token leaked via email forwarding
  - Mitigation: One-time exchange token (15-min TTL, single-use) -> opaque session cookie. URL is clean after redirect. `Referrer-Policy: no-referrer` on all preview responses.

- **Risk**: Memory pressure from concurrent activities on Y1 Consumption
  - Mitigation: Per existing rules: no two heavy activities in parallel. RehostAssetsToR2 processes max 2 concurrent binary downloads. BuildLocalizedSiteContent runs max 2 locales in parallel. SiteFactExtractor is pure compute (no binary downloads).

---

## Success Criteria

- [ ] New agent activation produces preview email, agent previews site, approves, completes Stripe checkout, site goes live
- [ ] Generated site has all 9 sections + 5 legal pages, in every locale the agent speaks
- [ ] Bilingual agent's Spanish site reads naturally, not machine-translated
- [ ] Two same-brokerage agents: second activation < 50% time and cost of first (warm start)
- [ ] Agent adds custom domain, follows CNAME instructions, site reachable at custom hostname with SSL within 10 minutes
- [ ] Agent reachable at platform subdomain, brokerage subpage, AND custom domain simultaneously with correct canonical tags
- [ ] PR preview builds for agent-site template changes continue working unchanged
- [ ] Per-activation Claude cost <= $1.20 (monolingual) / <= $1.60 (bilingual), observable in Grafana
- [ ] No OOM crashes during activation (DriveIndex <= 400 MB peak)
- [ ] Legal pages include "review with counsel" disclaimer
- [ ] Existing accounts (jenise-buckalew, safari-homes, glr) migrated to KV-backed content
- [ ] Every SS17 compliance rule has automated test
- [ ] Every SS16.3 endpoint has documented auth and unauthenticated integration test
- [ ] `VoicedContentGenerator` (sealed, no interface) is only Claude caller for voiced fields (arch test)
- [ ] PR preview builds render bundled test fixtures without touching production KV

---

**Key files referenced in this plan (absolute paths on this machine)**:

- Spec: `C:\Users\Edward.Rosado\Real-Estate-Star\docs\superpowers\specs\2026-04-12-agent-site-comprehensive-design.md`
- Plan output: `C:\Users\Edward.Rosado\Real-Estate-Star\docs\superpowers\plans\2026-04-16-agent-site-execution-plan.md`
- Existing empty Cloudflare csproj: `C:\Users\Edward.Rosado\Real-Estate-Star\apps\api\RealEstateStar.Clients\RealEstateStar.Clients.Cloudflare\RealEstateStar.Clients.Cloudflare.csproj`
- Existing empty Stripe csproj: `C:\Users\Edward.Rosado\Real-Estate-Star\apps\api\RealEstateStar.Clients\RealEstateStar.Clients.Stripe\`
- Existing AccountConfigService: `C:\Users\Edward.Rosado\Real-Estate-Star\apps\api\RealEstateStar.DataServices\Config\AccountConfigService.cs`
- Existing IAccountConfigService: `C:\Users\Edward.Rosado\Real-Estate-Star\apps\api\RealEstateStar.Domain\Shared\Interfaces\Storage\IAccountConfigService.cs`
- Existing IDistributedContentCache: `C:\Users\Edward.Rosado\Real-Estate-Star\apps\api\RealEstateStar.Domain\Shared\Interfaces\IDistributedContentCache.cs`
- Existing ClaudeDiagnostics: `C:\Users\Edward.Rosado\Real-Estate-Star\apps\api\RealEstateStar.Clients\RealEstateStar.Clients.Anthropic\ClaudeDiagnostics.cs`
- Existing OpenTelemetryExtensions: `C:\Users\Edward.Rosado\Real-Estate-Star\apps\api\RealEstateStar.Api\Diagnostics\OpenTelemetryExtensions.cs`
- Existing ActivationOrchestratorFunction: `C:\Users\Edward.Rosado\Real-Estate-Star\apps\api\RealEstateStar.Functions\Activation\ActivationOrchestratorFunction.cs`
- Existing Workers.Shared: `C:\Users\Edward.Rosado\Real-Estate-Star\apps\api\RealEstateStar.Workers\RealEstateStar.Workers.Shared\`
- Agent-site templates: `C:\Users\Edward.Rosado\Real-Estate-Star\apps\agent-site\features\templates\`
- Agent-site config: `C:\Users\Edward.Rosado\Real-Estate-Star\apps\agent-site\features\config\`
- Agent-site middleware: `C:\Users\Edward.Rosado\Real-Estate-Star\apps\agent-site\middleware.ts`