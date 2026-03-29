---
name: dotnet-api-multi-project-architecture
description: Reference architecture for .NET API backends — max isolation, every non-Api project depends only on Domain, architecture enforced via csproj + ArchUnit tests
type: reference
---

# .NET API Multi-Project Architecture

Each project answers exactly one question:

| Project | Question | Example |
|---------|----------|---------|
| **Domain** | "What should happen?" | Process lead → enrich → notify → enqueue CMA |
| **DataServices** | "Where do we store this?" | Route to Drive or local based on config, manage folders |
| **Data** | "How do we physically read/write?" | Local filesystem provider, GDrive provider, noop provider |
| **Notifications** | "Who needs to know?" | Email the agent, send WhatsApp to seller |
| **Api** | "Who's asking?" | HTTP request → validate → authorize → hand to Domain |
| **Workers.*** | "How do we do it?" | Dequeue → process → handle errors |
| **Clients.*** | "What external services?" | Meta Graph API, Claude API, ScraperAPI, Stripe |

## Dependency Flow (maximum isolation)

```
Domain         → nothing (owns ALL interfaces)
Data           → Domain only
Clients.*      → Domain only (implements Domain client interfaces, own internal DTOs)
DataServices   → Domain only
Notifications  → Domain only
Workers.Shared → Domain only
Workers.*      → Domain + Workers.Shared (Workers.Leads also refs Workers.Cma + Workers.HomeSearch for channel dispatch)
Api            → everything (sole composition root, DI wiring)
```

**Key:** Every non-Api project has at most 2-3 deps. No project outside Api knows Clients.*, Data, or DataServices exist. Domain defines ALL contracts — storage, client, sender interfaces. Api wires all implementations via DI.

## Actual Project List (19 production + 23 test projects)

### Production Projects
```
RealEstateStar.Domain/                ← Pure models, enums, ALL interfaces, business rules. ZERO deps.
  Shared/Interfaces/Storage/          ← IFileStorageProvider, IAccountConfigService
  Shared/Interfaces/External/         ← IGwsService, IProfileScraperService
  Shared/Interfaces/Senders/          ← IEmailNotifier, IWhatsAppSender
  Shared/Models/                      ← AccountConfig, DeletionAuditEntry, DriveActivityEvent
  Shared/Markdown/                    ← YamlFrontmatterParser (generic)
  Leads/                              ← Lead models, ILeadStore, ILeadNotifier, ILeadEnricher, LeadMarkdownRenderer
  Cma/                                ← CMA models, ICmaAnalyzer, ICmaPdfGenerator, ICompAggregator, CompAggregator
  HomeSearch/                         ← IHomeSearchProvider, HomeSearchMarkdownRenderer
  Privacy/                            ← IDeletionAuditLog, IMarketingConsentLog
  WhatsApp/                           ← WhatsApp models, IConversationHandler, IWhatsAppAuditService
  Billing/                            ← IStripeService
  Onboarding/                         ← OnboardingSession, OnboardingStateMachine, ISessionStore

RealEstateStar.Data/                  ← Physical file storage providers
  LocalStorageProvider.cs             ← implements IFileStorageProvider (local filesystem)
  GDriveStorageProvider.cs            ← implements IFileStorageProvider (Google Drive)
  NoopFileStorageProvider.cs          ← implements IFileStorageProvider (testing)

RealEstateStar.DataServices/          ← Storage orchestration
  Config/AccountConfigService.cs      ← implements IAccountConfigService
  Leads/GDriveLeadStore.cs            ← implements ILeadStore
  Leads/LeadPaths.cs                  ← Drive folder path structure (NOTE: should move from Domain here)
  Privacy/                            ← DeletionAuditLog, MarketingConsentLog, DriveChangeMonitor, DriveActivityParser
  WhatsApp/                           ← WhatsAppAuditService, ConversationLogger, ConversationLogRenderer,
                                         WebhookQueueService, IdempotencyStore, WhatsAppAuditEntry (ITableEntity)
  Onboarding/                         ← JsonFileSessionStore, EncryptingSessionStoreDecorator, ProfileScraperService

RealEstateStar.Notifications/         ← How to tell people things happened
  Leads/MultiChannelLeadNotifier.cs   ← implements ILeadNotifier (calls IEmailNotifier, IWhatsAppSender)
  Leads/CascadingAgentNotifier.cs     ← Google Chat + email cascade
  Leads/LeadChatCardRenderer.cs       ← Google Chat card formatting
  Leads/NoopEmailNotifier.cs          ← stub until email channel built
  Cma/CmaSellerNotifier.cs            ← implements ICmaNotifier
  HomeSearch/HomeSearchBuyerNotifier.cs
  WhatsApp/WhatsAppNotifier.cs        ← implements IWhatsAppNotifier
  WhatsApp/DisabledWhatsAppNotifier.cs ← null-object for unconfigured WhatsApp

RealEstateStar.Workers.Shared/        ← Base classes for worker pipelines
  IWorkerStep.cs, WorkerStepBase.cs   ← step interface + base class with OTel, logging
  ProcessingChannelBase.cs            ← Channel<T> base with bounded capacity
  BackgroundServiceHealthTracker.cs   ← tracks worker liveness for health checks
  DependencyInjection/                ← AddWorkerPipeline<TChannel, TWorker>() auto-DI

RealEstateStar.Workers.Leads/         ← Lead pipeline: enrich → notify → fan-out to CMA/HomeSearch
RealEstateStar.Workers.Cma/           ← CMA pipeline: fetch comps → analyze → PDF → notify
RealEstateStar.Workers.HomeSearch/    ← Home search: search → curate → notify
RealEstateStar.Workers.WhatsApp/      ← Webhook processing: dequeue → handle → respond → log

RealEstateStar.Clients.Anthropic/     ← Claude API (currently no separate impl — API calls inline)
RealEstateStar.Clients.Scraper/       ← ScraperAPI (currently no separate impl)
RealEstateStar.Clients.WhatsApp/      ← Meta Graph API → WhatsAppApiClient implements IWhatsAppSender
RealEstateStar.Clients.GDrive/        ← Google Drive API (currently no separate impl)
RealEstateStar.Clients.Gmail/         ← Gmail API (currently no separate impl)
RealEstateStar.Clients.GoogleOAuth/   ← Google OAuth2 (currently no separate impl)
RealEstateStar.Clients.Stripe/        ← Stripe API (currently no separate impl)
RealEstateStar.Clients.Cloudflare/    ← Cloudflare API (currently no separate impl)
RealEstateStar.Clients.Turnstile/     ← Cloudflare Turnstile (currently no separate impl)
RealEstateStar.Clients.Azure/         ← Azure Table Storage (currently no separate impl)
RealEstateStar.Clients.Gws/           ← Google Workspace CLI → GwsCliRunner implements IGwsService

RealEstateStar.Api/                   ← Thin HTTP surface + sole composition root
  Features/Leads/                     ← Submit, OptOut, DeleteData, RequestDeletion, Subscribe endpoints
  Features/Billing/                   ← StripeWebhookEndpoint, StripeService
  Features/Onboarding/                ← Endpoints, ChatService, GoogleOAuthService, Tools/*
  Features/WhatsApp/                  ← Webhook endpoints
  Infrastructure/                     ← IEndpoint, HMAC middleware, DnsResolver
  Health/                             ← All health checks
  Diagnostics/                        ← OpenTelemetryExtensions (cross-cutting)
```

### Test Projects (23)
```
tests/
  RealEstateStar.Domain.Tests/        (148 tests)
  RealEstateStar.Data.Tests/          (23 tests)
  RealEstateStar.DataServices.Tests/  (261 tests)
  RealEstateStar.Notifications.Tests/ (82 tests)
  RealEstateStar.Workers.Leads.Tests/ (26 tests)
  RealEstateStar.Workers.Cma.Tests/   (68 tests)
  RealEstateStar.Workers.HomeSearch.Tests/ (21 tests)
  RealEstateStar.Workers.WhatsApp.Tests/  (31 tests)
  RealEstateStar.Clients.WhatsApp.Tests/  (17 tests)
  RealEstateStar.Clients.Gws.Tests/      (11 tests)
  RealEstateStar.Clients.{others}.Tests/  (empty scaffolds for future tests)
  RealEstateStar.Api.Tests/           (462 tests)
  RealEstateStar.Architecture.Tests/  (29 tests — dependency matrix enforcement)
  RealEstateStar.TestUtilities/       (shared fakes, builders, fixtures — NOT a test project)
```

## Architecture Enforcement

1. **Compile-time (free):** `.csproj` `<ProjectReference>` — unauthorized deps don't compile
2. **CI-time:** 41 architecture tests in `tests/RealEstateStar.Architecture.Tests/`:
   - **31 assembly-level reflection tests** (`DependencyTests.cs`):
     - Domain has zero project dependencies
     - 19 InlineData theory cases for per-project allowed deps
     - Api allowed dependencies whitelist
     - Only Api may reference Clients.*, DataServices, Data, Notifications
     - Domain interfaces are not duplicated outside Domain
     - Workers do not reference ASP.NET Core
     - No circular dependencies between projects
     - All Domain exported types are public
     - Clients do not reference other Clients
     - Workers do not reference Data, DataServices, or Notifications
   - **10 NetArchTest type-level layer tests** (`LayerTests.cs`):
     - Domain types don't depend on DataServices, Notifications, Workers, Clients, or ASP.NET Core
     - DataServices types don't depend on Notifications or Workers
     - Notifications types don't depend on DataServices or Workers
     - Domain interfaces live in Domain namespace only

## Known Plan Deviations (to address in follow-ups)

- **IFileStorageProvider kept intact** (plan said split into ILocalFileProvider + IGDriveClient) — existing abstraction works, splitting is a breaking change best done separately
- **Worker step pattern not fully implemented** — Workers.Shared base classes exist but workers still use inline private methods. IWorkerStep/WorkerStepBase are scaffolded but unused. AddWorkerPipeline<> is dead code.
- **LeadPaths in Domain** — should be in DataServices (storage concern, not domain concept)
- **Many Clients.* are empty scaffolds** — implementations still inline in Api. Will be extracted as each client is refactored.

## Client DTO Boundary

Clients define their own internal request/response DTOs for HTTP communication. The Domain interface they implement uses Domain types. The Client maps between its internal DTOs and Domain types within its own implementation. No other project ever sees Client DTOs.

Each client that calls a rate-limited or unreliable API includes its own `*ResiliencePolicies.cs` with Polly retry + circuit breaker. These are registered with `IHttpClientFactory` in Api's DI wiring.

## Key Rules

- **Domain owns ALL interfaces**: storage, client, sender — no interface defined outside Domain
- **Api is the sole composition root**: only project that sees everything; wires DI bindings
- **Domain has zero dependencies**: no ASP.NET, no HttpClient, no storage (exception: Microsoft.Extensions.Logging.Abstractions for CompAggregator)
- **Clients implement Domain interfaces**: own internal DTOs, map to Domain types internally
- **Workers call Domain interfaces**: never reference Data, DataServices, Notifications, or Clients
- **Notifications call Domain sender interfaces**: never reference Clients.* directly
- **DataServices orchestrates storage**: calls Domain provider interfaces, Api wires implementations
- **Data is pure providers**: physical read/write only, no business logic
- **Diagnostics live with their domain**: each area owns its OTel metrics/traces
- **Tests are per-project**: `tests/RealEstateStar.{Project}.Tests/` + shared `TestUtilities`
- **Architecture tests enforce deps**: compile-time via csproj, CI-time via ArchUnit tests
- **InternalsVisibleTo**: each production project adds `[InternalsVisibleTo("RealEstateStar.{Project}.Tests")]`
