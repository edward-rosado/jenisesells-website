# API Project Restructure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Split the monolithic `RealEstateStar.Api` (~160 production files, ~108 test files) into 19 isolated .NET projects with maximum dependency isolation, enforced at compile-time and CI-time.

**Architecture:** Every non-Api project depends only on Domain (plus Workers.Shared for Workers.*). Api is the sole composition root. Domain owns ALL interfaces. See `docs/superpowers/specs/2026-03-21-api-project-restructure-design.md` for the full spec.

**Tech Stack:** .NET 10, xUnit, Moq, FluentAssertions, QuestPDF, Polly, Azure.Data.Tables, coverlet

**TDD Requirement:** RED → GREEN → REFACTOR for all new code. 100% branch coverage required on every project.

**Branch:** Create `refactor/api-project-restructure` from `main`.

---

## Scope Check

This plan covers a single large refactor of one subsystem (the .NET API backend). While there are 19 target projects, they all live in one solution and share a single migration path. The plan is structured as 14 sequential phases matching the spec's migration strategy — each phase compiles and passes tests before moving on.

## Parallelization Strategy

Many tasks within a phase are independent and can run in parallel worktrees:

- **Phase 3 (Domain):** All domain subfolders (Leads, Cma, HomeSearch, Privacy, WhatsApp, Billing, Onboarding, Shared) can move in parallel
- **Phase 6 (Clients):** All 11 client projects are independent — can extract in parallel batches
- **Phase 9 (Workers):** Each worker pipeline is independent after Workers.Shared exists
- **Phase 12 (Tests):** Each test project migration is independent

Tasks that MUST be sequential:
- Phase 1 (scaffold) → Phase 2 (arch tests) → Phase 3 (Domain) — everything depends on Domain existing
- Phase 3 (Domain) → Phases 4-9 — all depend on Domain interfaces
- Phase 8 (Workers.Shared) → Phase 9 (Workers.*) — workers depend on shared base classes

## File Structure

### New Projects to Create (under `apps/api/`)

```
apps/api/
  RealEstateStar.Domain/                    ← NEW: Pure models, interfaces, rules
  RealEstateStar.Data/                      ← NEW: Physical providers
  RealEstateStar.DataServices/              ← NEW: Storage orchestration
  RealEstateStar.Notifications/             ← NEW: Notification delivery
  RealEstateStar.Workers.Shared/            ← NEW: Pipeline base classes
  RealEstateStar.Workers.Leads/             ← NEW: Lead pipeline
  RealEstateStar.Workers.Cma/               ← NEW: CMA pipeline
  RealEstateStar.Workers.HomeSearch/         ← NEW: Home search pipeline
  RealEstateStar.Workers.WhatsApp/          ← NEW: WhatsApp webhook pipeline
  RealEstateStar.Clients.Anthropic/         ← NEW: Claude API client
  RealEstateStar.Clients.Scraper/           ← NEW: ScraperAPI client
  RealEstateStar.Clients.WhatsApp/          ← NEW: Meta Graph API client
  RealEstateStar.Clients.GDrive/            ← NEW: Google Drive client
  RealEstateStar.Clients.Gmail/             ← NEW: Gmail client
  RealEstateStar.Clients.GoogleOAuth/       ← NEW: Google OAuth client
  RealEstateStar.Clients.Stripe/            ← NEW: Stripe client
  RealEstateStar.Clients.Cloudflare/        ← NEW: Cloudflare client
  RealEstateStar.Clients.Turnstile/         ← NEW: Turnstile client
  RealEstateStar.Clients.Azure/             ← NEW: Azure Table Storage client
  RealEstateStar.Clients.Gws/              ← NEW: GWS CLI wrapper
  RealEstateStar.Api/                       ← EXISTING: Becomes thin HTTP layer
  tests/
    RealEstateStar.Domain.Tests/            ← NEW
    RealEstateStar.Data.Tests/              ← NEW
    RealEstateStar.DataServices.Tests/      ← NEW
    RealEstateStar.Notifications.Tests/     ← NEW
    RealEstateStar.Workers.Leads.Tests/     ← NEW
    RealEstateStar.Workers.Cma.Tests/       ← NEW
    RealEstateStar.Workers.HomeSearch.Tests/ ← NEW
    RealEstateStar.Workers.WhatsApp.Tests/  ← NEW
    RealEstateStar.Clients.Anthropic.Tests/ ← NEW
    RealEstateStar.Clients.Scraper.Tests/   ← NEW
    RealEstateStar.Clients.WhatsApp.Tests/  ← NEW
    RealEstateStar.Clients.GDrive.Tests/    ← NEW
    RealEstateStar.Clients.Gmail.Tests/     ← NEW
    RealEstateStar.Clients.GoogleOAuth.Tests/ ← NEW
    RealEstateStar.Clients.Stripe.Tests/    ← NEW
    RealEstateStar.Clients.Cloudflare.Tests/ ← NEW
    RealEstateStar.Clients.Turnstile.Tests/  ← NEW
    RealEstateStar.Clients.Azure.Tests/     ← NEW
    RealEstateStar.Clients.Gws.Tests/      ← NEW
    RealEstateStar.Api.Tests/               ← EXISTING: Slimmed down
    RealEstateStar.Architecture.Tests/      ← NEW: Dependency enforcement
    RealEstateStar.TestUtilities/           ← NEW: Shared test helpers
```

### Complete File Migration Map

Each line shows: `current location` → `new project/path`

#### Domain (models + interfaces + business rules)

```
# Shared models
Common/AccountConfig.cs                                    → Domain/Shared/Models/AccountConfig.cs
Features/Leads/DeletionAuditEntry.cs                       → Domain/Shared/Models/DeletionAuditEntry.cs
Features/Leads/DriveActivityEvent.cs                       → Domain/Shared/Models/DriveActivityEvent.cs
Features/Leads/DriveChangeResult.cs                        → Domain/Shared/Models/DriveChangeResult.cs

# Shared interfaces — Storage
Services/IAccountConfigService.cs                          → Domain/Shared/Interfaces/Storage/IAccountConfigService.cs
Services/Storage/IFileStorageProvider.cs                    → DELETE (replaced by ILocalFileProvider + IGDriveClient)

# Shared interfaces — Clients (NEW — extracted from inline usage)
(new)                                                      → Domain/Shared/Interfaces/Clients/IAnthropicClient.cs
(new)                                                      → Domain/Shared/Interfaces/Clients/IScraperClient.cs
(new)                                                      → Domain/Shared/Interfaces/Clients/IStripeClient.cs
(new)                                                      → Domain/Shared/Interfaces/Clients/ICloudflareClient.cs
(new)                                                      → Domain/Shared/Interfaces/Clients/ITurnstileClient.cs
(new)                                                      → Domain/Shared/Interfaces/Clients/IGDriveClient.cs
(new)                                                      → Domain/Shared/Interfaces/Clients/IGmailClient.cs
(new)                                                      → Domain/Shared/Interfaces/Clients/IGoogleOAuthClient.cs
(new)                                                      → Domain/Shared/Interfaces/Clients/IAzureTableClient.cs
(new)                                                      → Domain/Shared/Interfaces/Clients/IGwsCliRunner.cs

# Shared interfaces — Senders (NEW)
(new)                                                      → Domain/Shared/Interfaces/Senders/IEmailSender.cs
Features/WhatsApp/Services/IWhatsAppClient.cs              → Domain/Shared/Interfaces/Senders/IWhatsAppSender.cs (RENAME)

# Shared models (NEW)
(new)                                                      → Domain/Shared/Models/StepProgress.cs

# Shared interfaces — Storage (NEW)
(new)                                                      → Domain/Shared/Interfaces/Storage/ILocalFileProvider.cs
(new)                                                      → Domain/Shared/Interfaces/Storage/IStepProgressStore.cs

# Shared markdown
Features/Leads/YamlFrontmatterParser.cs                    → Domain/Shared/Markdown/YamlFrontmatterParser.cs

# Leads domain
Features/Leads/Lead.cs                                     → Domain/Leads/Models/Lead.cs
Features/Leads/LeadType.cs                                 → Domain/Leads/Models/LeadType.cs
Features/Leads/LeadStatus.cs                               → Domain/Leads/Models/LeadStatus.cs
Features/Leads/LeadScore.cs                                → Domain/Leads/Models/LeadScore.cs
Features/Leads/BuyerDetails.cs                             → Domain/Leads/Models/BuyerDetails.cs
Features/Leads/SellerDetails.cs                            → Domain/Leads/Models/SellerDetails.cs
Features/Leads/LeadEnrichment.cs                           → Domain/Leads/Models/LeadEnrichment.cs
Features/Leads/LeadNotification.cs                         → Domain/Leads/Models/LeadNotification.cs
Features/Leads/MarketingConsent.cs                         → Domain/Leads/Models/MarketingConsent.cs
Features/Leads/HomeSearchCriteria.cs                       → Domain/Leads/Models/HomeSearchCriteria.cs
Features/Leads/Listing.cs                                  → Domain/Leads/Models/Listing.cs
Features/Leads/DeleteResult.cs                             → Domain/Leads/Models/DeleteResult.cs
Features/Leads/Services/ILeadStore.cs                      → Domain/Leads/Interfaces/ILeadStore.cs
Features/Leads/Services/ILeadNotifier.cs                   → Domain/Leads/Interfaces/ILeadNotifier.cs
Features/Leads/Services/ILeadEnricher.cs                   → Domain/Leads/Interfaces/ILeadEnricher.cs
Features/Leads/Services/ILeadDataDeletion.cs               → Domain/Leads/Interfaces/ILeadDataDeletion.cs
Features/Leads/LeadMarkdownRenderer.cs                     → Domain/Leads/Markdown/LeadMarkdownRenderer.cs
Diagnostics/LeadDiagnostics.cs                             → Domain/Leads/LeadDiagnostics.cs

# CMA domain
Features/Leads/Cma/CmaAnalysis.cs                         → Domain/Cma/Models/CmaAnalysis.cs
Features/Leads/Cma/CmaResult.cs                            → Domain/Cma/Models/CmaResult.cs
Features/Leads/Cma/Comp.cs                                 → Domain/Cma/Models/Comp.cs
Features/Leads/Cma/CompSearchRequest.cs                    → Domain/Cma/Models/CompSearchRequest.cs
Features/Leads/Cma/ReportType.cs                           → Domain/Cma/Models/ReportType.cs
Features/Leads/Cma/CompAggregator.cs                       → Domain/Cma/Services/CompAggregator.cs
Features/Leads/Cma/ICmaAnalyzer.cs                         → Domain/Cma/Interfaces/ICmaAnalyzer.cs
Features/Leads/Cma/ICmaNotifier.cs                         → Domain/Cma/Interfaces/ICmaNotifier.cs
Features/Leads/Cma/ICmaPdfGenerator.cs                     → Domain/Cma/Interfaces/ICmaPdfGenerator.cs
Features/Leads/Cma/ICompAggregator.cs                      → Domain/Cma/Interfaces/ICompAggregator.cs
Features/Leads/Cma/ICompSource.cs                          → Domain/Cma/Interfaces/ICompSource.cs
Diagnostics/CmaDiagnostics.cs                              → Domain/Cma/CmaDiagnostics.cs

# HomeSearch domain
Features/Leads/Services/IHomeSearchProvider.cs             → Domain/HomeSearch/Interfaces/IHomeSearchProvider.cs
Features/Leads/Services/IHomeSearchNotifier.cs             → Domain/HomeSearch/Interfaces/IHomeSearchNotifier.cs
Features/Leads/Services/HomeSearchMarkdownRenderer.cs      → Domain/HomeSearch/Markdown/HomeSearchMarkdownRenderer.cs
Diagnostics/HomeSearchDiagnostics.cs                       → Domain/HomeSearch/HomeSearchDiagnostics.cs

# Privacy domain
Features/Leads/Services/IDeletionAuditLog.cs               → Domain/Privacy/Interfaces/IDeletionAuditLog.cs
Features/Leads/Services/IMarketingConsentLog.cs            → Domain/Privacy/Interfaces/IMarketingConsentLog.cs

# WhatsApp domain
Features/WhatsApp/WhatsAppTypes.cs                         → Domain/WhatsApp/Models/WhatsAppTypes.cs
Features/WhatsApp/WhatsAppAuditEntry.cs                    → Domain/WhatsApp/Models/WhatsAppAuditEntry.cs
Features/WhatsApp/Services/IConversationHandler.cs         → Domain/WhatsApp/Interfaces/IConversationHandler.cs
Features/WhatsApp/Services/IConversationLogger.cs          → Domain/WhatsApp/Interfaces/IConversationLogger.cs
Features/WhatsApp/Services/IIntentClassifier.cs            → Domain/WhatsApp/Interfaces/IIntentClassifier.cs
Features/WhatsApp/Services/IResponseGenerator.cs           → Domain/WhatsApp/Interfaces/IResponseGenerator.cs
Features/WhatsApp/Services/IWebhookQueueService.cs         → Domain/WhatsApp/Interfaces/IWebhookQueueService.cs
Features/WhatsApp/Services/IWhatsAppAuditService.cs        → Domain/WhatsApp/Interfaces/IWhatsAppAuditService.cs
Features/WhatsApp/Services/IWhatsAppNotifier.cs            → Domain/WhatsApp/Interfaces/IWhatsAppNotifier.cs
Features/WhatsApp/WhatsAppDiagnostics.cs                   → Domain/WhatsApp/WhatsAppDiagnostics.cs

# Billing domain
Features/Onboarding/Services/IStripeService.cs             → Domain/Billing/Interfaces/IStripeService.cs

# Onboarding domain
Features/Onboarding/OnboardingSession.cs                   → Domain/Onboarding/Models/OnboardingSession.cs
Features/Onboarding/OnboardingState.cs                     → Domain/Onboarding/Models/OnboardingState.cs
Features/Onboarding/ChatMessage.cs                         → Domain/Onboarding/Models/ChatMessage.cs
Features/Onboarding/ScrapedProfile.cs                      → Domain/Onboarding/Models/ScrapedProfile.cs
Features/Onboarding/GoogleTokens.cs                        → Domain/Onboarding/Models/GoogleTokens.cs
Features/Onboarding/Services/ISessionStore.cs              → Domain/Onboarding/Interfaces/ISessionStore.cs
Features/Onboarding/Tools/IOnboardingTool.cs               → Domain/Onboarding/Interfaces/IOnboardingTool.cs
Features/Onboarding/Tools/IToolDispatcher.cs               → Domain/Onboarding/Interfaces/IToolDispatcher.cs
Features/Onboarding/Tools/IDnsResolver.cs                  → Domain/Onboarding/Interfaces/IDnsResolver.cs
Features/Onboarding/Tools/IProcessRunner.cs                → Domain/Onboarding/Interfaces/IProcessRunner.cs
Features/Onboarding/Tools/IProfileScraper.cs               → Domain/Onboarding/Interfaces/IProfileScraperService.cs
Features/Onboarding/Tools/ISiteDeployService.cs            → Domain/Onboarding/Interfaces/ISiteDeployService.cs
Features/Onboarding/Services/OnboardingStateMachine.cs     → Domain/Onboarding/Services/OnboardingStateMachine.cs
Features/Onboarding/OnboardingHelpers.cs                   → Domain/Onboarding/OnboardingHelpers.cs
```

#### Data (physical providers)

```
Services/Storage/LocalFileStorageProvider.cs               → Data/LocalFileProvider.cs (CONSOLIDATE w/ LocalStorageProvider)
Services/Storage/LocalStorageProvider.cs                   → Data/LocalFileProvider.cs (CONSOLIDATE)
Services/Storage/NoopFileStorageProvider.cs                → Data/InMemoryFileProvider.cs (REPLACE)
```

#### DataServices (storage orchestration)

```
Services/AccountConfigService.cs                           → DataServices/Config/AccountConfigService.cs
Features/Leads/Services/FileLeadStore.cs                   → DataServices/Leads/LeadStore.cs (MERGE w/ GDriveLeadStore)
Features/Leads/Services/GDriveLeadStore.cs                 → DataServices/Leads/LeadStore.cs (MERGE into single routing class)
Features/Leads/LeadPaths.cs                                → DataServices/Leads/LeadPaths.cs
Services/Gws/LeadBriefData.cs                              → DataServices/Leads/LeadBriefData.cs
Features/Leads/Services/GDriveLeadDataDeletion.cs          → DataServices/Leads/LeadDataDeletion.cs
Features/Leads/Services/DeletionAuditLog.cs                → DataServices/Privacy/DeletionAuditLog.cs
Features/Leads/Services/MarketingConsentLog.cs             → DataServices/Privacy/MarketingConsentLog.cs
Features/Leads/Services/DriveActivityParser.cs             → DataServices/Privacy/DriveActivityParser.cs
Features/Leads/Services/DriveChangeMonitor.cs              → DataServices/Privacy/DriveChangeMonitor.cs
Features/WhatsApp/Services/AzureWhatsAppAuditService.cs    → DataServices/WhatsApp/WhatsAppAuditService.cs
Features/WhatsApp/Services/DisabledWhatsAppAuditService.cs → DataServices/WhatsApp/DisabledWhatsAppAuditService.cs
Features/WhatsApp/Services/AzureWebhookQueueService.cs     → DataServices/WhatsApp/WebhookQueueService.cs
Features/WhatsApp/Services/WhatsAppIdempotencyStore.cs     → DataServices/WhatsApp/WhatsAppIdempotencyStore.cs
Features/WhatsApp/WhatsAppPaths.cs                         → DataServices/WhatsApp/WhatsAppPaths.cs
Features/WhatsApp/Services/ConversationLogger.cs           → DataServices/WhatsApp/ConversationLogger.cs
Features/WhatsApp/Services/ConversationLogRenderer.cs      → DataServices/WhatsApp/ConversationLogRenderer.cs
Features/Onboarding/Services/JsonFileSessionStore.cs       → DataServices/Onboarding/SessionStore.cs
Features/Onboarding/Services/EncryptingSessionStoreDecorator.cs → DataServices/Onboarding/EncryptingSessionStoreDecorator.cs
Features/Onboarding/Tools/ProfileScraperService.cs         → DataServices/Onboarding/ProfileScraperService.cs
(new)                                                      → DataServices/Progress/StepProgressStore.cs
```

#### Notifications

```
Features/Leads/Services/MultiChannelLeadNotifier.cs        → Notifications/Leads/MultiChannelLeadNotifier.cs
Features/Leads/Services/CascadingAgentNotifier.cs          → Notifications/Leads/CascadingAgentNotifier.cs
Features/Leads/Services/LeadChatCardRenderer.cs            → Notifications/Leads/LeadChatCardRenderer.cs
Features/Leads/Services/NoopEmailNotifier.cs               → Notifications/Leads/NoopEmailNotifier.cs
Features/Leads/Cma/CmaSellerNotifier.cs                    → Notifications/Cma/CmaSellerNotifier.cs
Features/Leads/Services/HomeSearchBuyerNotifier.cs         → Notifications/HomeSearch/HomeSearchBuyerNotifier.cs
Features/WhatsApp/Services/WhatsAppNotifier.cs             → Notifications/WhatsApp/WhatsAppNotifier.cs
Features/WhatsApp/Services/DisabledWhatsAppNotifier.cs     → Notifications/WhatsApp/DisabledWhatsAppNotifier.cs
```

#### Clients

```
# Clients.WhatsApp
Features/WhatsApp/Services/WhatsAppClient.cs               → Clients.WhatsApp/WhatsAppApiClient.cs
Features/WhatsApp/Services/WhatsAppResiliencePolicies.cs   → Clients.WhatsApp/WhatsAppResiliencePolicies.cs

# Clients.Gws
Services/Gws/GwsService.cs                                → Clients.Gws/GwsCliRunner.cs
Services/Gws/IGwsService.cs                               → DELETE (replaced by Domain/Shared/Interfaces/Clients/IGwsCliRunner.cs)

# Clients.Anthropic, Scraper, GDrive, Gmail, GoogleOAuth, Stripe, Cloudflare, Turnstile, Azure
# These are NEW — extracted from inline usage in current services.
# Each gets its own project with: Client.cs, Options.cs, Dto/ folder
```

#### Workers

```
# Workers.Shared (NEW — base classes)
(new)                                                      → Workers.Shared/IWorkerStep.cs
(new)                                                      → Workers.Shared/WorkerStepBase.cs
(new)                                                      → Workers.Shared/IProcessingChannel.cs
(new)                                                      → Workers.Shared/ProcessingChannelBase.cs
(new)                                                      → Workers.Shared/WorkerBase.cs
(new)                                                      → Workers.Shared/DependencyInjection/WorkerServiceCollectionExtensions.cs

# Workers.Leads
Features/Leads/Services/LeadProcessingChannel.cs           → Workers.Leads/LeadProcessingChannel.cs
Features/Leads/Services/LeadProcessingWorker.cs            → Workers.Leads/LeadProcessingWorker.cs (REWRITE to step pattern)
Features/Leads/Services/ScraperLeadEnricher.cs             → Workers.Leads/Steps/EnrichLeadStep.cs (ABSORB into step)
(new)                                                      → Workers.Leads/Steps/ScoreLeadStep.cs
(new)                                                      → Workers.Leads/Steps/StoreLeadStep.cs
(new)                                                      → Workers.Leads/Steps/NotifyAgentStep.cs
(new)                                                      → Workers.Leads/Steps/EnqueueCmaStep.cs
(new)                                                      → Workers.Leads/Steps/EnqueueHomeSearchStep.cs

# Workers.Cma
Features/Leads/Services/CmaProcessingChannel.cs            → Workers.Cma/CmaProcessingChannel.cs
Features/Leads/Services/CmaProcessingWorker.cs             → Workers.Cma/CmaProcessingWorker.cs (REWRITE to step pattern)
Features/Leads/Cma/ScraperCompSource.cs                    → Workers.Cma/Steps/FetchCompsStep.cs (ABSORB into step)
Features/Leads/Cma/ClaudeCmaAnalyzer.cs                    → Workers.Cma/Steps/AnalyzeCompsStep.cs (ABSORB into step)
Features/Leads/Cma/CmaPdfGenerator.cs                      → Workers.Cma/Steps/GeneratePdfStep.cs (ABSORB into step)
(new)                                                      → Workers.Cma/Steps/StoreCmaStep.cs
(new)                                                      → Workers.Cma/Steps/NotifySellerStep.cs

# Workers.HomeSearch
Features/Leads/Services/HomeSearchProcessingChannel.cs     → Workers.HomeSearch/HomeSearchProcessingChannel.cs
Features/Leads/Services/HomeSearchProcessingWorker.cs      → Workers.HomeSearch/HomeSearchProcessingWorker.cs (REWRITE)
Features/Leads/Services/ScraperHomeSearchProvider.cs       → Workers.HomeSearch/Steps/SearchListingsStep.cs (ABSORB)
(new)                                                      → Workers.HomeSearch/Steps/CurateListingsStep.cs
(new)                                                      → Workers.HomeSearch/Steps/StoreResultsStep.cs
(new)                                                      → Workers.HomeSearch/Steps/NotifyBuyerStep.cs

# Workers.WhatsApp
Features/WhatsApp/Services/WebhookProcessorWorker.cs       → Workers.WhatsApp/WebhookProcessorWorker.cs
Features/WhatsApp/Services/ConversationHandler.cs          → Workers.WhatsApp/ConversationHandler.cs
Features/WhatsApp/Services/WhatsAppRetryJob.cs             → Workers.WhatsApp/WhatsAppRetryJob.cs
Features/WhatsApp/Services/NoopIntentClassifier.cs         → Workers.WhatsApp/NoopIntentClassifier.cs
Features/WhatsApp/Services/NoopResponseGenerator.cs        → Workers.WhatsApp/NoopResponseGenerator.cs
(new)                                                      → Workers.WhatsApp/Steps/ClassifyIntentStep.cs
(new)                                                      → Workers.WhatsApp/Steps/GenerateResponseStep.cs
(new)                                                      → Workers.WhatsApp/Steps/SendReplyStep.cs
(new)                                                      → Workers.WhatsApp/Steps/LogConversationStep.cs
```

#### Api (stays — thin HTTP layer)

```
# STAYS in Api
Features/Leads/Submit/*                                    → Api/Features/Leads/Submit/ (stays)
Features/Leads/OptOut/*                                    → Api/Features/Leads/OptOut/ (stays)
Features/Leads/DeleteData/*                                → Api/Features/Leads/DeleteData/ (stays)
Features/Leads/RequestDeletion/*                           → Api/Features/Leads/RequestDeletion/ (stays)
Features/Leads/Subscribe/*                                 → Api/Features/Leads/Subscribe/ (stays)
Features/Leads/LeadMappers.cs                              → Api/Features/Leads/LeadMappers.cs (stays, HTTP-facing only)
Features/Onboarding/* endpoints                            → Api/Features/Onboarding/ (stays)
Features/Onboarding/Services/OnboardingChatService.cs      → Api/Features/Onboarding/Services/ (stays)
Features/Onboarding/Services/GoogleOAuthService.cs         → Api/Features/Onboarding/Services/ (stays)
Features/Onboarding/Services/StripeService.cs              → Api/Features/Billing/Services/StripeService.cs
Features/Onboarding/Services/TrialExpiryService.cs         → Api/Features/Billing/Services/TrialExpiryService.cs (stays in Api)
Features/Onboarding/Webhooks/StripeWebhookEndpoint.cs      → Api/Features/Billing/Webhooks/StripeWebhookEndpoint.cs
Features/Onboarding/Tools/ToolDispatcher.cs                → Api/Features/Onboarding/Tools/ (stays)
Features/Onboarding/Tools/DeploySiteTool.cs                → Api/Features/Onboarding/Tools/ (stays)
Features/Onboarding/Tools/ScrapeUrlTool.cs                 → Api/Features/Onboarding/Tools/ (stays)
Features/Onboarding/Tools/CreateStripeSessionTool.cs       → Api/Features/Onboarding/Tools/ (stays)
Features/Onboarding/Tools/GoogleAuthCardTool.cs            → Api/Features/Onboarding/Tools/ (stays)
Features/Onboarding/Tools/SendWhatsAppWelcomeTool.cs       → Api/Features/Onboarding/Tools/ (stays)
Features/Onboarding/Tools/SetBrandingTool.cs               → Api/Features/Onboarding/Tools/ (stays)
Features/Onboarding/Tools/UpdateProfileTool.cs             → Api/Features/Onboarding/Tools/ (stays)
Features/Onboarding/Tools/ProcessRunner.cs                 → Api/Services/ProcessRunner.cs
Features/Onboarding/Tools/SiteDeployService.cs             → Api/Services/SiteDeployService.cs
Features/Onboarding/Tools/CloudflareOptions.cs             → Api/Features/Onboarding/Tools/ (stays, later moves to Clients.Cloudflare)
Features/WhatsApp/Webhook/*                                → Api/Features/WhatsApp/Webhook/ (stays)
Features/WhatsApp/WhatsAppMappers.cs                       → Api/Features/WhatsApp/WhatsAppMappers.cs (stays, HTTP-facing)
Features/Onboarding/OnboardingMappers.cs                   → Api/Features/Onboarding/OnboardingMappers.cs (stays)
Infrastructure/*                                           → Api/Infrastructure/ (stays)
Middleware/*                                               → Api/Middleware/ (stays)
Health/*                                                   → Api/Health/ (stays)
Diagnostics/OpenTelemetryExtensions.cs                     → Api/Diagnostics/ (stays)
Logging/LoggingExtensions.cs                               → Api/Logging/ (stays)
Program.cs                                                 → Api/Program.cs (stays, heavily refactored)
```

#### Files to DELETE

```
# Storage abstractions replaced by Domain interfaces
Services/Storage/IFileStorageProvider.cs                    → DELETE (replaced by ILocalFileProvider + IGDriveClient)
Services/Storage/GDriveStorageProvider.cs                   → DELETE (replaced by DataServices/Leads/LeadStore routing)
Services/Storage/LocalFileStorageProvider.cs                → DELETE (consolidated into Data/LocalFileProvider.cs)
Services/Storage/LocalStorageProvider.cs                    → DELETE (consolidated into Data/LocalFileProvider.cs)
Services/Storage/NoopFileStorageProvider.cs                 → DELETE (replaced by Data/InMemoryFileProvider.cs)

# Gws + email + polly
Services/Gws/IGwsService.cs                                → DELETE (replaced by IGwsCliRunner)
Features/Leads/Services/IEmailNotifier.cs                  → DELETE (replaced by IEmailSender + ILeadNotifier)
Infrastructure/PollyPolicies.cs                            → DELETE (each client owns its own policies)

# Tests for deleted interfaces/files
tests/.../IFileStorageProviderContractTests.cs             → DELETE (contract tests for deleted IFileStorageProvider)
tests/.../PollyPolicyTests.cs                              → DELETE (tests for deleted centralized PollyPolicies)
tests/.../LocalFileStorageProviderTests.cs                 → DELETE (replaced by Data.Tests/LocalFileProviderTests.cs)
tests/.../LocalStorageProviderTests.cs                     → DELETE (replaced by Data.Tests/LocalFileProviderTests.cs)
tests/.../NoopFileStorageProviderTests.cs                  → DELETE (replaced by Data.Tests/InMemoryFileProviderTests.cs)
```

---

## Phase 1: Scaffold All Projects

### Task 1: Create solution structure and empty projects

**Files:**
- Modify: `apps/api/RealEstateStar.Api.sln`
- Create: 19 new `.csproj` files + `tests/` projects + `RealEstateStar.TestUtilities`

- [ ] **Step 1: Create branch from main**

```bash
git checkout main && git pull
git checkout -b refactor/api-project-restructure
```

- [ ] **Step 2: Create all production project directories**

Create each project with `dotnet new classlib` under `apps/api/`:

```
RealEstateStar.Domain
RealEstateStar.Data
RealEstateStar.DataServices
RealEstateStar.Notifications
RealEstateStar.Workers.Shared
RealEstateStar.Workers.Leads
RealEstateStar.Workers.Cma
RealEstateStar.Workers.HomeSearch
RealEstateStar.Workers.WhatsApp
RealEstateStar.Clients.Anthropic
RealEstateStar.Clients.Scraper
RealEstateStar.Clients.WhatsApp
RealEstateStar.Clients.GDrive
RealEstateStar.Clients.Gmail
RealEstateStar.Clients.GoogleOAuth
RealEstateStar.Clients.Stripe
RealEstateStar.Clients.Cloudflare
RealEstateStar.Clients.Turnstile
RealEstateStar.Clients.Azure
RealEstateStar.Clients.Gws
```

- [ ] **Step 3: Set correct `<ProjectReference>` in each .csproj**

Follow the dependency matrix exactly:
- **Domain:** No `<ProjectReference>` (zero deps)
- **Data:** `→ Domain`
- **DataServices:** `→ Domain`
- **Notifications:** `→ Domain`
- **Workers.Shared:** `→ Domain`
- **Workers.Leads/Cma/HomeSearch/WhatsApp:** `→ Domain + Workers.Shared`
- **All Clients.*:** `→ Domain`
- **Api:** `→ ALL projects` (add references incrementally as projects get content)

Add `<InternalsVisibleTo Include="RealEstateStar.{Project}.Tests" />` to each .csproj.

- [ ] **Step 4: Create all test project directories**

Create each with `dotnet new xunit` under `apps/api/tests/`:

```
RealEstateStar.Domain.Tests
RealEstateStar.Data.Tests
RealEstateStar.DataServices.Tests
RealEstateStar.Notifications.Tests
RealEstateStar.Workers.Leads.Tests
RealEstateStar.Workers.Cma.Tests
RealEstateStar.Workers.HomeSearch.Tests
RealEstateStar.Workers.WhatsApp.Tests
RealEstateStar.Clients.Anthropic.Tests
RealEstateStar.Clients.Scraper.Tests
RealEstateStar.Clients.WhatsApp.Tests
RealEstateStar.Clients.GDrive.Tests
RealEstateStar.Clients.Gmail.Tests
RealEstateStar.Clients.GoogleOAuth.Tests
RealEstateStar.Clients.Stripe.Tests
RealEstateStar.Clients.Cloudflare.Tests
RealEstateStar.Clients.Turnstile.Tests
RealEstateStar.Clients.Azure.Tests
RealEstateStar.Clients.Gws.Tests
RealEstateStar.Api.Tests (already exists — will be slimmed)
RealEstateStar.Architecture.Tests
RealEstateStar.TestUtilities (classlib, not xunit)
```

Each test project references its production project + `RealEstateStar.TestUtilities`.

- [ ] **Step 5: Add all projects to the solution**

```bash
cd apps/api
dotnet sln add RealEstateStar.Domain/RealEstateStar.Domain.csproj
# ... repeat for all projects
dotnet sln add tests/RealEstateStar.Architecture.Tests/RealEstateStar.Architecture.Tests.csproj
```

- [ ] **Step 6: Verify solution builds**

```bash
dotnet build apps/api/RealEstateStar.Api.sln
```

Expected: All projects compile (they're empty classlibs with correct refs).

- [ ] **Step 7: Commit**

```bash
git add apps/api/
git commit -m "refactor: scaffold 19 new projects with dependency matrix"
```

---

## Phase 2: Architecture Tests

### Task 2: Write dependency enforcement tests (TDD — tests first)

**Files:**
- Create: `apps/api/tests/RealEstateStar.Architecture.Tests/DependencyTests.cs`

- [ ] **Step 1: Write the architecture test file**

```csharp
// tests/RealEstateStar.Architecture.Tests/DependencyTests.cs
namespace RealEstateStar.Architecture.Tests;

public class DependencyTests
{
    [Fact]
    public void Domain_has_no_project_dependencies()
    {
        var assembly = typeof(Domain.Leads.Models.Lead).Assembly;
        var violations = assembly.GetReferencedAssemblies()
            .Where(a => a.Name!.StartsWith("RealEstateStar"))
            .Select(a => a.Name!)
            .ToList();
        Assert.Empty(violations);
    }

    [Theory]
    [InlineData("RealEstateStar.Data", new[] { "Domain" })]
    [InlineData("RealEstateStar.DataServices", new[] { "Domain" })]
    [InlineData("RealEstateStar.Notifications", new[] { "Domain" })]
    [InlineData("RealEstateStar.Workers.Shared", new[] { "Domain" })]
    [InlineData("RealEstateStar.Workers.Leads", new[] { "Domain", "Workers.Shared" })]
    [InlineData("RealEstateStar.Workers.Cma", new[] { "Domain", "Workers.Shared" })]
    [InlineData("RealEstateStar.Workers.HomeSearch", new[] { "Domain", "Workers.Shared" })]
    [InlineData("RealEstateStar.Workers.WhatsApp", new[] { "Domain", "Workers.Shared" })]
    [InlineData("RealEstateStar.Clients.Anthropic", new[] { "Domain" })]
    [InlineData("RealEstateStar.Clients.Scraper", new[] { "Domain" })]
    [InlineData("RealEstateStar.Clients.WhatsApp", new[] { "Domain" })]
    [InlineData("RealEstateStar.Clients.GDrive", new[] { "Domain" })]
    [InlineData("RealEstateStar.Clients.Gmail", new[] { "Domain" })]
    [InlineData("RealEstateStar.Clients.GoogleOAuth", new[] { "Domain" })]
    [InlineData("RealEstateStar.Clients.Stripe", new[] { "Domain" })]
    [InlineData("RealEstateStar.Clients.Cloudflare", new[] { "Domain" })]
    [InlineData("RealEstateStar.Clients.Turnstile", new[] { "Domain" })]
    [InlineData("RealEstateStar.Clients.Azure", new[] { "Domain" })]
    [InlineData("RealEstateStar.Clients.Gws", new[] { "Domain" })]
    public void Project_only_depends_on_allowed_projects(string projectName, string[] allowedSuffixes)
    {
        // Load by name from test bin output (all projects copied there)
        var assembly = Assembly.Load(projectName);
        var allowed = allowedSuffixes
            .Select(s => $"RealEstateStar.{s}")
            .Append(projectName)
            .ToHashSet();

        var violations = assembly.GetReferencedAssemblies()
            .Where(a => a.Name!.StartsWith("RealEstateStar"))
            .Where(a => !allowed.Contains(a.Name!))
            .Select(a => a.Name!)
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Api_is_the_only_project_that_references_Clients()
    {
        var productionAssemblies = Directory.GetFiles(AppContext.BaseDirectory, "RealEstateStar.*.dll")
            .Select(Assembly.LoadFrom)
            .Where(a => !a.GetName().Name!.Contains("Api"))
            .Where(a => !a.GetName().Name!.Contains("Tests"))
            .Where(a => !a.GetName().Name!.Contains("TestUtilities"));

        foreach (var assembly in productionAssemblies)
        {
            var clientRefs = assembly.GetReferencedAssemblies()
                .Where(a => a.Name!.Contains("Clients"))
                .Select(a => a.Name!)
                .ToList();

            Assert.True(clientRefs.Count == 0,
                $"{assembly.GetName().Name} references {string.Join(", ", clientRefs)} — only Api may reference Clients.*");
        }
    }
}
```

- [ ] **Step 2: Run tests — they should pass (empty projects have no violations)**

```bash
dotnet test apps/api/tests/RealEstateStar.Architecture.Tests/ -v minimal
```

- [ ] **Step 3: Commit**

```bash
git add apps/api/tests/RealEstateStar.Architecture.Tests/
git commit -m "test: add ArchUnit-style dependency enforcement tests"
```

---

## Phase 3: Move Domain

### Task 3: Extract Domain project

This is the foundation — every other project depends on Domain.

**Strategy:** Move files by domain subfolder. After each subfolder, update namespaces, verify the old project still compiles (via `using` aliases or temporary re-exports), and run all tests.

- [ ] **Step 1: Move Shared models to Domain/Shared/Models/**

Move `AccountConfig.cs`, `DeletionAuditEntry.cs`, `DriveActivityEvent.cs`, `DriveChangeResult.cs`. Update namespaces to `RealEstateStar.Domain.Shared.Models`. Add `using RealEstateStar.Domain.Shared.Models;` in old consumers.

- [ ] **Step 2: Create NEW Domain interfaces — Storage, Clients, Senders**

Write `ILocalFileProvider`, `IStepProgressStore`, `IAnthropicClient`, `IScraperClient`, `IStripeClient`, `ICloudflareClient`, `ITurnstileClient`, `IGDriveClient`, `IGmailClient`, `IGoogleOAuthClient`, `IAzureTableClient`, `IGwsCliRunner`, `IEmailSender`, `IWhatsAppSender`. Each interface is extracted from existing inline usage patterns in current code. Write tests for each interface contract (compile test at minimum).

- [ ] **Step 3: Move Lead models + interfaces**

Move all Lead models (`Lead.cs`, `LeadType.cs`, etc.) → `Domain/Leads/Models/`. Move `ILeadStore`, `ILeadNotifier`, `ILeadEnricher`, `ILeadDataDeletion` → `Domain/Leads/Interfaces/`. Move `LeadMarkdownRenderer` → `Domain/Leads/Markdown/`. Move `LeadDiagnostics` → `Domain/Leads/`. Update namespaces. Run `dotnet build && dotnet test`.

- [ ] **Step 4: Move CMA models + interfaces**

Move all CMA models and interfaces → `Domain/Cma/`. Move `CmaDiagnostics`. Update namespaces. Build + test.

- [ ] **Step 5: Move HomeSearch, Privacy, WhatsApp, Billing, Onboarding domain types**

Repeat the pattern for each domain area. Move interfaces and models. Move `OnboardingStateMachine`, `OnboardingHelpers`. Update namespaces. Build + test after each.

- [ ] **Step 6: Move YamlFrontmatterParser to Domain/Shared/Markdown/**

This is used by multiple domain areas. Update namespace. Build + test.

- [ ] **Step 7: Verify Domain has zero project references**

Check `RealEstateStar.Domain.csproj` has no `<ProjectReference>` entries.

- [ ] **Step 8: Run full test suite**

```bash
dotnet test apps/api/RealEstateStar.Api.sln
```

- [ ] **Step 9: Commit**

```bash
git commit -m "refactor: extract Domain project — all models, interfaces, business rules"
```

---

## Phase 4: Create DataServices

### Task 4: Extract DataServices project

**Files:** See DataServices section of the migration map above.

- [ ] **Step 1: Move AccountConfigService → DataServices/Config/**

Update namespace. Add `using RealEstateStar.Domain.Shared.Interfaces.Storage;`. Build + test.

- [ ] **Step 2: Merge FileLeadStore + GDriveLeadStore → DataServices/Leads/LeadStore**

Create single `LeadStore` that routes based on config. Move `LeadPaths`, `LeadBriefData`, `LeadDataDeletion`. Write tests for routing logic (local vs GDrive). Build + test.

- [ ] **Step 3: Move Privacy services → DataServices/Privacy/**

`DeletionAuditLog`, `MarketingConsentLog`, `DriveActivityParser`, `DriveChangeMonitor`. Update namespaces. Build + test.

- [ ] **Step 4: Move WhatsApp data services → DataServices/WhatsApp/**

`WhatsAppAuditService`, `DisabledWhatsAppAuditService`, `WebhookQueueService`, `WhatsAppIdempotencyStore`, `WhatsAppPaths`, `ConversationLogger`, `ConversationLogRenderer`. Update namespaces. Build + test.

- [ ] **Step 5: Move Onboarding data services → DataServices/Onboarding/**

`SessionStore` (was `JsonFileSessionStore`), `EncryptingSessionStoreDecorator`, `ProfileScraperService`. Update namespaces. Build + test.

- [ ] **Step 6: Create StepProgressStore → DataServices/Progress/**

NEW file — implements `IStepProgressStore`. Write TDD tests first. Build + test.

- [ ] **Step 7: Run full suite, commit**

```bash
dotnet test apps/api/RealEstateStar.Api.sln
git commit -m "refactor: extract DataServices project — storage orchestration"
```

---

## Phase 5: Create Data

### Task 5: Extract Data project

- [ ] **Step 1: Consolidate LocalFileStorageProvider + LocalStorageProvider → Data/LocalFileProvider**

Take best features of both (path traversal protection, atomic writes, logging). Implement `ILocalFileProvider`. TDD.

- [ ] **Step 2: Create InMemoryFileProvider → Data/InMemoryFileProvider**

Replaces `NoopFileStorageProvider`. Implements `ILocalFileProvider`. TDD.

- [ ] **Step 3: Delete old storage files**

Delete `IFileStorageProvider.cs`, `GDriveStorageProvider.cs`, `LocalFileStorageProvider.cs`, `LocalStorageProvider.cs`, `NoopFileStorageProvider.cs`. Update all consumers to use new interfaces.

- [ ] **Step 4: Run full suite, commit**

```bash
git commit -m "refactor: extract Data project — pure physical providers"
```

---

## Phase 6: Extract Clients (parallelizable — each client is independent)

### Task 6a: Extract Clients.Turnstile

Simplest client — start here as the template.

- [ ] **Step 1: Write ITurnstileClient contract tests (TDD RED)**
- [ ] **Step 2: Create TurnstileClient, TurnstileOptions, Dto/ (GREEN)**
- [ ] **Step 3: Write TurnstileClient unit tests with MockHttpMessageHandler**
- [ ] **Step 4: Build + test, commit**

### Task 6b: Extract Clients.Scraper

- [ ] Repeat pattern: IScraperClient tests → ScraperClient + Options + Dto/ → unit tests → commit

### Task 6c: Extract Clients.Anthropic

- [ ] Repeat pattern for IAnthropicClient

### Task 6d: Extract Clients.Azure

- [ ] Repeat pattern for IAzureTableClient

### Task 6e: Extract Clients.Gws

- [ ] Move GwsService → GwsCliRunner. Implement IGwsCliRunner. Delete IGwsService.

### Task 6f: Extract Clients.GDrive

- [ ] Extract from GDriveStorageProvider + GwsService Drive operations → GDriveClient + GDriveResiliencePolicies

### Task 6g: Extract Clients.Gmail

- [ ] Extract Gmail operations → GmailClient. Implement IGmailClient + IEmailSender.

### Task 6h: Extract Clients.GoogleOAuth

- [ ] Extract from GoogleOAuthService → GoogleOAuthClient. Implement IGoogleOAuthClient.

### Task 6i: Extract Clients.Stripe

- [ ] Extract Stripe API calls → StripeClient. Implement IStripeClient.

### Task 6j: Extract Clients.Cloudflare

- [ ] Extract from DeploySiteTool Cloudflare logic → CloudflareClient. Implement ICloudflareClient.

### Task 6k: Extract Clients.WhatsApp

- [ ] Move WhatsAppClient → WhatsAppApiClient. Move WhatsAppResiliencePolicies. Implement IWhatsAppSender. Own Dto/ folder.

**After all clients extracted:**

- [ ] **Step: Delete Infrastructure/PollyPolicies.cs** — each client owns its own policies now
- [ ] **Step: Run full suite, commit**

```bash
git commit -m "refactor: extract 11 Clients.* projects — one per external API"
```

---

## Phase 7: Move Notifications

### Task 7: Extract Notifications project

- [ ] **Step 1: Move MultiChannelLeadNotifier, CascadingAgentNotifier, LeadChatCardRenderer, NoopEmailNotifier → Notifications/Leads/**
- [ ] **Step 2: Move CmaSellerNotifier → Notifications/Cma/**
- [ ] **Step 3: Move HomeSearchBuyerNotifier → Notifications/HomeSearch/**
- [ ] **Step 4: Move WhatsAppNotifier, DisabledWhatsAppNotifier → Notifications/WhatsApp/**
- [ ] **Step 5: Delete IEmailNotifier** — replaced by IEmailSender + ILeadNotifier combination
- [ ] **Step 6: Verify Notifications depends only on Domain (check .csproj)**
- [ ] **Step 7: Run full suite, commit**

```bash
git commit -m "refactor: extract Notifications project — delivery implementations"
```

---

## Phase 8: Create Workers.Shared

### Task 8: Build Workers.Shared base classes (TDD — all new code)

- [ ] **Step 1: Write IWorkerStep<TRequest, TResponse> interface**
- [ ] **Step 2: Write WorkerStepBase tests (TDD RED) — verify OTel span, logging, error handling**
- [ ] **Step 3: Implement WorkerStepBase (GREEN)**
- [ ] **Step 4: Write IProcessingChannel + ProcessingChannelBase**
- [ ] **Step 5: Write WorkerBase tests (TDD RED) — checkpoint/resume, skip completed, re-execute on deserialization failure, delete on success**
- [ ] **Step 6: Implement WorkerBase with RunOrResumeAsync (GREEN)**
- [ ] **Step 7: Write AddWorkerPipeline extension method + tests**
- [ ] **Step 8: Add QuestPDF wrapper in Workers.Shared/Pdf/ if needed by CMA pipeline**
- [ ] **Step 9: Run full suite, commit**

```bash
git commit -m "feat: add Workers.Shared — step interface, base classes, checkpoint/resume"
```

---

## Phase 9: Move Workers (parallelizable — each pipeline is independent)

### Task 9a: Extract Workers.Leads

- [ ] **Step 1: Move LeadProcessingChannel**
- [ ] **Step 2: Write EnrichLeadStep tests (TDD RED)**
- [ ] **Step 3: Implement EnrichLeadStep — absorbs ScraperLeadEnricher logic (GREEN)**
- [ ] **Step 4: Repeat TDD for ScoreLeadStep, StoreLeadStep, NotifyAgentStep, EnqueueCmaStep, EnqueueHomeSearchStep**
- [ ] **Step 5: Rewrite LeadProcessingWorker to use WorkerBase + steps**
- [ ] **Step 6: Write LeadWorkerMappers**
- [ ] **Step 7: Build + test, commit**

### Task 9b: Extract Workers.Cma

- [ ] **Step 1: Move CmaProcessingChannel**
- [ ] **Step 2: TDD for FetchCompsStep (absorbs ScraperCompSource + CompAggregator)**
- [ ] **Step 3: TDD for AnalyzeCompsStep (absorbs ClaudeCmaAnalyzer)**
- [ ] **Step 4: TDD for GeneratePdfStep (absorbs CmaPdfGenerator)**
- [ ] **Step 5: TDD for StoreCmaStep, NotifySellerStep**
- [ ] **Step 6: Rewrite CmaProcessingWorker to use WorkerBase + steps**
- [ ] **Step 7: Build + test, commit**

### Task 9c: Extract Workers.HomeSearch

- [ ] **Step 1: Move HomeSearchProcessingChannel**
- [ ] **Step 2: TDD for SearchListingsStep (absorbs ScraperHomeSearchProvider)**
- [ ] **Step 3: TDD for CurateListingsStep, StoreResultsStep, NotifyBuyerStep**
- [ ] **Step 4: Rewrite HomeSearchProcessingWorker**
- [ ] **Step 5: Build + test, commit**

### Task 9d: Extract Workers.WhatsApp

- [ ] **Step 1: Move WebhookProcessorWorker (adapt to channel pattern)**
- [ ] **Step 2: Move ConversationHandler, WhatsAppRetryJob, NoopIntentClassifier, NoopResponseGenerator**
- [ ] **Step 3: TDD for ClassifyIntentStep, GenerateResponseStep, SendReplyStep, LogConversationStep**
- [ ] **Step 4: Build + test, commit**

```bash
git commit -m "refactor: extract 4 Workers.* projects — step-based pipelines"
```

---

## Phase 10: Clean Up Api

### Task 10: Slim down Api to thin HTTP layer

- [ ] **Step 1: Remove all emptied folders** from Api (old Services/, Features/Leads/Services/, Features/Leads/Cma/, etc.)
- [ ] **Step 2: Move StripeWebhookEndpoint → Api/Features/Billing/Webhooks/**
- [ ] **Step 3: Move StripeService, TrialExpiryService → Api/Features/Billing/Services/**
- [ ] **Step 4: Move ProcessRunner, SiteDeployService → Api/Services/**
- [ ] **Step 5: Refactor Program.cs** — use `AddWorkerPipeline<>()` for each pipeline, clean up DI registrations to reference new project namespaces
- [ ] **Step 6: Verify Api references ALL projects in .csproj**
- [ ] **Step 7: Run full suite, commit**

```bash
git commit -m "refactor: slim Api to thin HTTP layer + composition root"
```

---

## Phase 11: Move Tests

### Task 11: Redistribute tests to per-project test projects

**Strategy:** Move test files to mirror the new production structure. Update `using` namespaces. Update mock setups for renamed interfaces.

- [ ] **Step 1: Create TestUtilities** — move `TestHelpers/MockHttpMessageHandler.cs`, `TestHelpers/InMemoryWebhookQueueService.cs`, extract `TestWebApplicationFactory` no-op stubs into shared builders

- [ ] **Step 2: Move Domain tests** → `tests/RealEstateStar.Domain.Tests/`
```
Features/Leads/LeadTests.cs                          → Domain.Tests/Leads/LeadTests.cs
Features/Leads/LeadMarkdownRendererTests.cs          → Domain.Tests/Leads/LeadMarkdownRendererTests.cs
Features/Leads/YamlFrontmatterParserTests.cs         → Domain.Tests/Shared/YamlFrontmatterParserTests.cs
Features/Leads/Cma/CompAggregatorTests.cs            → Domain.Tests/Cma/CompAggregatorTests.cs
Features/Leads/Services/HomeSearchMarkdownRendererTests.cs → Domain.Tests/HomeSearch/HomeSearchMarkdownRendererTests.cs
Features/Onboarding/OnboardingSessionTests.cs        → Domain.Tests/Onboarding/OnboardingSessionTests.cs
Features/Onboarding/OnboardingHelpersTests.cs        → Domain.Tests/Onboarding/OnboardingHelpersTests.cs
Features/Onboarding/GoogleTokensTests.cs             → Domain.Tests/Onboarding/GoogleTokensTests.cs
Features/Onboarding/Services/StateMachineTests.cs    → Domain.Tests/Onboarding/StateMachineTests.cs
Features/WhatsApp/WhatsAppTypesTests.cs              → Domain.Tests/WhatsApp/WhatsAppTypesTests.cs
Features/WhatsApp/WhatsAppDiagnosticsTests.cs        → Domain.Tests/WhatsApp/WhatsAppDiagnosticsTests.cs
Diagnostics/LeadDiagnosticsTests.cs                  → Domain.Tests/Leads/LeadDiagnosticsTests.cs
```

- [ ] **Step 3: Move DataServices tests** → `tests/RealEstateStar.DataServices.Tests/`
```
Services/AccountConfigServiceTests.cs                → DataServices.Tests/Config/AccountConfigServiceTests.cs
Features/Leads/Services/FileLeadStoreTests.cs        → DataServices.Tests/Leads/LeadStoreTests.cs (adapt)
Features/Leads/Services/GDriveLeadStoreTests.cs      → DataServices.Tests/Leads/LeadStoreTests.cs (merge)
Features/Leads/Services/GDriveLeadDataDeletionTests.cs → DataServices.Tests/Leads/LeadDataDeletionTests.cs
Features/Leads/LeadPathsTests.cs                     → DataServices.Tests/Leads/LeadPathsTests.cs
Features/Leads/Services/DeletionAuditLogTests.cs     → DataServices.Tests/Privacy/DeletionAuditLogTests.cs
Features/Leads/Services/DriveActivityParserTests.cs  → DataServices.Tests/Privacy/DriveActivityParserTests.cs
Features/Leads/Services/DriveChangeMonitorTests.cs   → DataServices.Tests/Privacy/DriveChangeMonitorTests.cs
Features/Leads/Services/MarketingConsentLogTests.cs  → DataServices.Tests/Privacy/MarketingConsentLogTests.cs
Features/Onboarding/Services/SessionStoreTests.cs    → DataServices.Tests/Onboarding/SessionStoreTests.cs
Features/Onboarding/Services/SessionStoreEncryptionTests.cs → DataServices.Tests/Onboarding/SessionStoreEncryptionTests.cs
Features/Onboarding/Tools/ProfileScraperTests.cs     → DataServices.Tests/Onboarding/ProfileScraperTests.cs
Features/WhatsApp/Services/AzureWhatsAppAuditServiceTests.cs → DataServices.Tests/WhatsApp/WhatsAppAuditServiceTests.cs
Features/WhatsApp/Services/DisabledWhatsAppAuditServiceTests.cs → DataServices.Tests/WhatsApp/DisabledWhatsAppAuditServiceTests.cs
Features/WhatsApp/Services/ConversationLoggerTests.cs → DataServices.Tests/WhatsApp/ConversationLoggerTests.cs
Features/WhatsApp/Services/ConversationLogRendererTests.cs → DataServices.Tests/WhatsApp/ConversationLogRendererTests.cs
Features/WhatsApp/Services/WebhookQueueServiceTests.cs → DataServices.Tests/WhatsApp/WebhookQueueServiceTests.cs
Features/WhatsApp/Services/WhatsAppIdempotencyStoreTests.cs → DataServices.Tests/WhatsApp/WhatsAppIdempotencyStoreTests.cs
Features/WhatsApp/WhatsAppPathsTests.cs              → DataServices.Tests/WhatsApp/WhatsAppPathsTests.cs
```

- [ ] **Step 4: Write new Data tests** → `tests/RealEstateStar.Data.Tests/`
```
# Old tests are DELETED (see Files to DELETE). Write fresh TDD tests for new implementations:
(new)                                                → Data.Tests/LocalFileProviderTests.cs
(new)                                                → Data.Tests/InMemoryFileProviderTests.cs
```

- [ ] **Step 5: Move Notifications tests** → `tests/RealEstateStar.Notifications.Tests/`
```
Features/Leads/Submit/MultiChannelLeadNotifierTests.cs → Notifications.Tests/Leads/MultiChannelLeadNotifierTests.cs
Features/Leads/Services/CascadingAgentNotifierTests.cs → Notifications.Tests/Leads/CascadingAgentNotifierTests.cs
Features/Leads/Submit/LeadChatCardRendererTests.cs   → Notifications.Tests/Leads/LeadChatCardRendererTests.cs
Features/Leads/Services/NoopEmailNotifierTests.cs    → Notifications.Tests/Leads/NoopEmailNotifierTests.cs
Features/WhatsApp/Services/WhatsAppNotifierTests.cs  → Notifications.Tests/WhatsApp/WhatsAppNotifierTests.cs
Features/WhatsApp/Services/DisabledWhatsAppNotifierTests.cs → Notifications.Tests/WhatsApp/DisabledWhatsAppNotifierTests.cs
```

- [ ] **Step 6: Move Clients tests** → per-client `tests/RealEstateStar.Clients.*.Tests/`
```
Features/WhatsApp/Services/WhatsAppClientTests.cs    → Clients.WhatsApp.Tests/WhatsAppApiClientTests.cs
Features/WhatsApp/Services/WhatsAppResiliencePolicyTests.cs → Clients.WhatsApp.Tests/WhatsAppResiliencePolicyTests.cs
Services/Gws/GwsServiceTests.cs                     → Clients.Gws.Tests/GwsCliRunnerTests.cs
Services/Storage/GDriveStorageProviderTests.cs       → Clients.GDrive.Tests/GDriveClientTests.cs (adapt)
Features/Leads/Cma/ScraperCompSourceTests.cs         → Clients.Scraper.Tests/ScraperClientTests.cs (adapt)
Features/Leads/Cma/ClaudeCmaAnalyzerTests.cs         → Clients.Anthropic.Tests/AnthropicClientTests.cs (adapt)
# Clients.Stripe.Tests, Clients.Gmail.Tests, Clients.GoogleOAuth.Tests,
# Clients.Cloudflare.Tests, Clients.Turnstile.Tests, Clients.Azure.Tests
# → NEW test files written during Phase 6 TDD (no existing tests to move)
```

- [ ] **Step 7: Move Worker tests** → per-worker `tests/RealEstateStar.Workers.*.Tests/`
```
Features/Leads/Services/LeadProcessingWorkerTests.cs → Workers.Leads.Tests/LeadProcessingWorkerTests.cs (adapt to step pattern)
Features/Leads/Submit/ScraperLeadEnricherTests.cs    → Workers.Leads.Tests/Steps/EnrichLeadStepTests.cs (adapt)
Features/Leads/Submit/ScraperHomeSearchProviderTests.cs → Workers.HomeSearch.Tests/Steps/SearchListingsStepTests.cs (adapt)
Features/Leads/Services/CmaProcessingWorkerTests.cs  → Workers.Cma.Tests/CmaProcessingWorkerTests.cs (adapt)
Features/Leads/Cma/CmaPdfGeneratorTests.cs           → Workers.Cma.Tests/Steps/GeneratePdfStepTests.cs (adapt)
Features/Leads/Services/HomeSearchProcessingWorkerTests.cs → Workers.HomeSearch.Tests/HomeSearchProcessingWorkerTests.cs (adapt)
Features/WhatsApp/Services/WebhookProcessorWorkerTests.cs → Workers.WhatsApp.Tests/WebhookProcessorWorkerTests.cs
Features/WhatsApp/Services/ConversationHandlerTests.cs → Workers.WhatsApp.Tests/ConversationHandlerTests.cs
Features/WhatsApp/Services/WhatsAppRetryJobTests.cs  → Workers.WhatsApp.Tests/WhatsAppRetryJobTests.cs
Features/WhatsApp/Services/NoopIntentClassifierTests.cs → Workers.WhatsApp.Tests/NoopIntentClassifierTests.cs
Features/WhatsApp/Services/NoopResponseGeneratorTests.cs → Workers.WhatsApp.Tests/NoopResponseGeneratorTests.cs
# Additional step tests written during Phase 9 TDD
```

- [ ] **Step 8: Keep in Api.Tests** — explicit list of tests that stay:
```
# Endpoint tests (STAY — they test HTTP layer)
Features/Leads/Submit/SubmitLeadEndpointTests.cs
Features/Leads/DeleteData/DeleteDataEndpointTests.cs
Features/Leads/OptOut/OptOutEndpointTests.cs
Features/Leads/RequestDeletion/RequestDeletionEndpointTests.cs
Features/Leads/Subscribe/SubscribeEndpointTests.cs
Features/Leads/LeadMappersTests.cs
Features/Leads/LeadSubmissionIntegrationTests.cs
Features/Onboarding/ConnectGoogle/GoogleOAuthCallbackEndpointTests.cs
Features/Onboarding/ConnectGoogle/StartGoogleOAuthEndpointTests.cs
Features/Onboarding/CreateSession/CreateSessionEndpointTests.cs
Features/Onboarding/GetSession/GetSessionEndpointTests.cs
Features/Onboarding/PostChat/PostChatEndpointTests.cs
Features/Onboarding/OnboardingMappersTests.cs
Features/Onboarding/Services/ChatServiceTests.cs
Features/Onboarding/Services/GoogleOAuthServiceTests.cs
Features/Onboarding/Services/StripeServiceTests.cs
Features/Onboarding/Services/TrialExpiryTests.cs
Features/Onboarding/Tools/ToolDispatcherTests.cs
Features/Onboarding/Tools/CreateStripeSessionToolTests.cs
Features/Onboarding/Tools/DeploySiteToolTests.cs
Features/Onboarding/Tools/GoogleAuthCardToolTests.cs
Features/Onboarding/Tools/ProcessRunnerTests.cs
Features/Onboarding/Tools/ScrapeUrlToolTests.cs
Features/Onboarding/Tools/SendWhatsAppWelcomeToolTests.cs
Features/Onboarding/Tools/SetBrandingToolTests.cs
Features/Onboarding/Tools/SiteDeployTests.cs
Features/Onboarding/Tools/UpdateProfileToolTests.cs
Features/Onboarding/Webhooks/StripeWebhookEndpointTests.cs
Features/WhatsApp/Webhook/ReceiveWebhook/ReceiveWebhookEndpointTests.cs
Features/WhatsApp/Webhook/ReceiveWebhook/WebhookPayloadTests.cs
Features/WhatsApp/Webhook/VerifyWebhook/VerifyWebhookEndpointTests.cs
Features/WhatsApp/WhatsAppMappersTests.cs
Common/AccountWhatsAppTests.cs
Diagnostics/OpenTelemetryExtensionsTests.cs
Health/BackgroundServiceHealthCheckTests.cs
Health/BackgroundServiceHealthTrackerTests.cs
Health/ClaudeApiHealthCheckTests.cs
Health/GoogleDriveHealthCheckTests.cs
Health/GwsCliHealthCheckTests.cs
Health/HealthCheckTests.cs
Health/ScraperApiHealthCheckTests.cs
Health/TurnstileHealthCheckTests.cs
Infrastructure/ApiKeyHmacMiddlewareTests.cs
Infrastructure/RateLimitingTests.cs
Integration/MiddlewarePipelineTests.cs
Integration/TestWebApplicationFactory.cs
Middleware/CorrelationIdMiddlewareTests.cs
```
- [ ] **Step 9: Delete old empty test folders**
- [ ] **Step 10: Run full suite**

```bash
dotnet test apps/api/RealEstateStar.Api.sln
```

- [ ] **Step 11: Commit**

```bash
git commit -m "refactor: redistribute tests to per-project test projects"
```

---

## Phase 12: Delete Old Empty Folders

### Task 12: Clean up

- [ ] **Step 1: Delete all empty directories** left behind after moves
- [ ] **Step 2: Delete obsolete files** listed in "Files to DELETE" section
- [ ] **Step 3: Run full suite to verify nothing breaks**
- [ ] **Step 4: Commit**

```bash
git commit -m "chore: delete empty folders and obsolete files"
```

---

## Phase 13: Clean Up DI Registrations

### Task 13: Verify Program.cs

- [ ] **Step 1: Audit Program.cs** — ensure no duplicate registrations (same interface registered in both old and new location)
- [ ] **Step 2: Verify AddWorkerPipeline calls** for all 4 pipelines
- [ ] **Step 3: Verify all Clients.* are registered** via `AddHttpClient<>()` with their Polly policies
- [ ] **Step 4: Verify DataServices registrations** — LeadStore, AccountConfigService, StepProgressStore, etc.
- [ ] **Step 5: Verify Notifications registrations** — all notifiers wired
- [ ] **Step 6: Run integration tests** to confirm DI wiring works end-to-end

```bash
dotnet test apps/api/tests/RealEstateStar.Api.Tests/ --filter "Integration"
```

- [ ] **Step 7: Commit**

```bash
git commit -m "refactor: clean up DI registrations in Program.cs"
```

---

## Phase 14: Coverage + Architecture Verification

### Task 14: Final verification

- [ ] **Step 1: Run architecture tests**

```bash
dotnet test apps/api/tests/RealEstateStar.Architecture.Tests/ -v normal
```

All dependency constraints must pass.

- [ ] **Step 2: Run full test suite with coverage**

```bash
bash apps/api/scripts/coverage.sh
```

Verify 100% branch coverage on all new code.

- [ ] **Step 3: Run coverage with `--low-only`**

```bash
bash apps/api/scripts/coverage.sh --low-only
```

Address any classes below 100%.

- [ ] **Step 4: Final build verification**

```bash
dotnet build apps/api/RealEstateStar.Api.sln --configuration Release
```

- [ ] **Step 5: Commit any final fixes**

```bash
git commit -m "test: achieve 100% branch coverage on all new projects"
```

- [ ] **Step 6: Create PR**

```bash
gh pr create --title "refactor: split monolithic API into 19 isolated projects" --body "..."
```

---

## Risk Mitigation

| Risk | Mitigation |
|------|-----------|
| Namespace changes break 100+ test files | Move tests in batches per domain area, run full suite after each batch |
| DI registration conflicts during partial migration | Keep both old and new registrations temporarily, delete old after test pass |
| Build time increase from 19 projects | Measure before/after with `time dotnet build`. If >2x, consider merging tiny Clients (e.g., Turnstile → Cloudflare) |
| Worker rewrite to step pattern changes behavior | Write characterization tests for current worker behavior BEFORE rewriting |
| StepProgress checkpoint is new code | Full TDD + property-based tests for serialization roundtrip |

## Notes for Implementers

1. **Always `dotnet build` + `dotnet test` after every file move.** A broken build wastes more time than the 10 seconds it takes to verify.
2. **Namespace convention:** `RealEstateStar.{Project}.{Subfolder}` — e.g., `RealEstateStar.Domain.Leads.Models`, `RealEstateStar.Workers.Shared`.
3. **When moving a file:** Update the namespace, add `using` for the new namespace in all consumers, build, test. Do NOT try to move 20 files at once.
4. **CancellationToken is REQUIRED** (no `= default`) — this is a standing project rule.
5. **Every log statement needs a unique `[PREFIX-NNN]` error code.**
6. **100% branch coverage.** No exceptions. Use `bash apps/api/scripts/coverage.sh --low-only` to find gaps.
