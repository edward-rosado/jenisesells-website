# Real Estate Star

A SaaS platform that automates real estate agent workflows — from lead response to contract drafting to website deployment.

## Monorepo Structure

```
apps/
  portal/          # Real Estate Star admin portal (Next.js 16)
  agent-site/      # White-label agent websites (Next.js 16)
  api/             # Backend API (.NET 10) — 21 isolated projects
    RealEstateStar.Api/              # HTTP layer + sole composition root (DI wiring)
    RealEstateStar.Domain/           # Pure models, interfaces, enums — ZERO deps
    RealEstateStar.Data/             # Physical file storage providers
    RealEstateStar.DataServices/     # Storage orchestration (routes GDrive vs local)
    RealEstateStar.Notifications/    # Delivery channels (email, WhatsApp)
    RealEstateStar.Functions/         # Azure Functions host (Durable orchestrators + activity wrappers)
    RealEstateStar.Workers.Shared/   # Pipeline base classes (ActivityBase, retry options)
    RealEstateStar.Workers.Leads/    # Lead processing pipeline
    RealEstateStar.Workers.Cma/      # CMA pipeline — tiered comp selection (5-comp target, 6-month recency), subject enrichment, PDF generation
    RealEstateStar.Workers.HomeSearch/ # Home search pipeline
    RealEstateStar.Workers.WhatsApp/ # WhatsApp message processing
    RealEstateStar.Clients.Anthropic/  # Claude API client
    RealEstateStar.Clients.Scraper/    # Web scraper client
    RealEstateStar.Clients.WhatsApp/   # WhatsApp API client
    RealEstateStar.Clients.GDrive/     # Google Drive client
    RealEstateStar.Clients.Gmail/      # Gmail client
    RealEstateStar.Clients.GDocs/      # Google Docs API client
    RealEstateStar.Clients.GSheets/    # Google Sheets API client
    RealEstateStar.Clients.GoogleOAuth/ # Shared OAuth token refresh (IOAuthRefresher impl)
    RealEstateStar.Clients.Stripe/     # Stripe client
    RealEstateStar.Clients.Cloudflare/ # Cloudflare client
    RealEstateStar.Clients.Turnstile/  # Turnstile client
    RealEstateStar.Clients.Azure/      # Azure Table Storage client
    RealEstateStar.Clients.RentCast/   # RentCast API client (comp data for CMA)
    RealEstateStar.Clients.Gws/        # GWS CLI wrapper
    tests/                             # 23 test projects (1:1 with production + Architecture.Tests + TestUtilities)
packages/
  domain/          # Types, interfaces, enums, correlation IDs — ZERO deps
  api-client/      # Typed API client (openapi-fetch + generated types)
  forms/           # LeadForm, CMA hooks, validation → domain
  legal/           # EqualHousingNotice, CookieConsent, LegalPageLayout → domain
  analytics/       # Telemetry, error reporting, Web Vitals → domain
skills/
  cma/             # Comparative Market Analysis generator
  contracts/       # State-specific contract drafting
  email/           # Multi-provider email sending
  deploy/          # Website deployment
config/
  accounts/{handle}/         # Per-tenant account config (account.json, content.json, legal/)
  agent.schema.json          # JSON Schema for agent profiles
prototype/         # Original jenisesellsnj.com static site
infra/             # Infrastructure and hosting config
docs/              # Design docs, onboarding, plans
```

### API Dependency Rules

```
Domain         → nothing (owns ALL interfaces)
Data           → Domain only
Clients.*      → Domain only (own internal DTOs)
DataServices   → Domain only
Notifications  → Domain only
Workers.Shared → Domain only
Workers.*      → Domain + Workers.Shared
Api            → everything (sole composition root)
```

Every non-Api project has at most 2 deps. Domain defines ALL contracts. Api wires all implementations via DI. Architecture is enforced at compile-time (csproj refs) and CI-time (ArchUnit tests in `tests/RealEstateStar.Architecture.Tests/`).

### Frontend Package Dependency Rules

```
domain         → nothing (types, interfaces, correlation IDs)
api-client     → domain only
forms          → domain only
legal          → domain only
analytics      → domain only
```

Every package has at most 1 internal dep (`domain`). Enforced by `scripts/validate-architecture.mjs` (CI) and `packages/domain/__tests__/architecture.test.ts` (test runner).

### Frontend Feature Structure

Both apps use `features/` folders with isolated modules:

**Platform:** `onboarding`, `billing`, `landing`, `status`, `shared`
**Agent-site:** `config`, `templates`, `sections`, `lead-capture`, `privacy`, `shared`

Rules:
- `app/` pages are thin composition roots — import from `features/`, no business logic
- Features cannot cross-import (enforced by ESLint `no-restricted-imports`)
- `features/shared/` can be imported by any feature; `shared/` cannot import from features
- Agent-site exception: `templates/ → sections/` (subsection barrels only, NOT top-level `sections/index.ts`)
- Barrel exports: named re-exports only (`export { X } from './X'`), never `export *`

### Feature Scope Map

#### Platform
| Feature | Can Import From | Cannot Import From |
|---------|----------------|-------------------|
| onboarding | shared/, all packages | billing, landing, status |
| billing | shared/, all packages | onboarding, landing, status |
| landing | shared/, all packages | onboarding, billing, status |
| status | shared/, all packages | onboarding, billing, landing |

#### Agent-Site
| Feature | Can Import From | Cannot Import From |
|---------|----------------|-------------------|
| config | all packages | templates, sections, lead-capture, privacy |
| templates | sections subsection barrels, config, packages | lead-capture, privacy, shared |
| sections | config, shared, all packages | templates, lead-capture, privacy |
| lead-capture | config, shared, all packages | templates, sections, privacy |
| privacy | config, shared, all packages | templates, sections, lead-capture |
| shared | config, all packages | templates, sections, lead-capture, privacy |

### Frontend Conventions

- Section components use inline `style={}` with CSS custom properties for runtime branding (NOT Tailwind className)
- Telemetry events use PascalCase enum (`Viewed`, `Started`, `Submitted`, `Succeeded`, `Failed`) matching backend `FormEvent`
- Use `reportError()` from `@real-estate-star/analytics`, not `console.error`
- Every API call includes `X-Correlation-ID` (auto-injected by api-client)
- Analytics ownership: platform = our GA4 keys (env var), agent-site = BYOK from account.json
- `generated/types.ts` in api-client is auto-generated — never hand-edit
- Dynamic template loading via `next/dynamic` — each template is a separate chunk

## Multi-Tenant Architecture

Every agent (tenant) has a config directory at `config/accounts/{handle}/` containing `account.json`, `content.json`, and `legal/` files.

**All skills read from agent config — never hardcode agent-specific data.**

### Loading an Agent Profile

When working on a skill, load the agent profile first:

```
1. Read config/accounts/{handle}/account.json
2. Use {agent.identity.*} for name, phone, email, brokerage, etc.
3. Use {agent.location.*} for state, service areas, office address
4. Use {agent.branding.*} for colors, fonts
5. Use {agent.integrations.*} for email provider, hosting, form handler
6. Use {agent.compliance.*} for state forms, licensing body, disclosures
```

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Portal | Next.js 16 |
| Agent Sites | Next.js 16 (white-label) |
| API | .NET 10 (21 isolated projects) |
| Agent Config | JSON + JSON Schema |
| PM | GitHub Issues + Projects |

## Key Conventions

- **Domain name**: The domain is `real-estate-star.com` (hyphenated). Use `real-estate-star` everywhere -- DNS, Azure resource names, Cloudflare, GitHub, etc. NEVER use `realestatestar` (no hyphens).
- **Commits**: Conventional commits (`feat:`, `fix:`, `docs:`, `chore:`, etc.)
- **Skills**: Reference agent config with `{agent.*}` variable syntax
- **Contracts**: State-specific templates live in `skills/contracts/templates/{STATE}/`
- **No hardcoding**: Agent identity, branding, and compliance data always come from config
- **API calls**: Platform uses shared `api` instance from `@/lib/api`. Agent-site passes HMAC headers per-request via `createApiClient()`. SSE streaming stays raw `fetch`. Correlation IDs are auto-injected.
- **Pitch decks**: `docs/pitch-decks/` contains both `.html` (presentation) and `.md` (source) for each deck. **Always update both files together** — they must stay in sync. The `.md` is the readable reference; the `.html` is the presentable version.
- **Locale is a first-class dimension**: Every feature that touches user-facing content MUST carry locale through the entire pipeline. This is a design principle, not an afterthought. See rules below.

### Locale-First Design Principle

Locale is a **first-class dimension** of every design — like `agentId` or `correlationId`, it must flow through every layer that produces, transforms, or delivers content to a human.

**Rules for new features:**
1. Every DTO that carries content to users MUST have a `Locale` property
2. Every DTO that carries synthesis results MUST have a `LocalizedSkills` property if the worker extracts per-language content
3. Every service method that drafts user-facing text (emails, PDFs, notifications) MUST accept a locale or AgentContext parameter
4. Every worker that analyzes agent communications MUST detect language and produce per-language output when sufficient data exists
5. Agent personality and voice profiles MUST capture per-language catchphrases, signature expressions, and cultural heritage
6. Welcome messages and lead emails MUST use the agent's authentic expressions in the contact's language — not generic translations

**Enforced by:** `LocaleTests.cs` in Architecture.Tests — reflection-based tests verify locale fields exist on all content-carrying DTOs. CI rejects PRs that drop locale fields.

**Why this matters:** Our agents serve bilingual communities. A Dominican agent's "¡Pa'lante!" is not interchangeable with a generic "¡Adelante!". The system must preserve and use each agent's authentic voice in every language they speak.

## Architecture Test Protection

**Architecture tests are IMMUTABLE unless the user explicitly approves a change.**

The following files in `tests/RealEstateStar.Architecture.Tests/` enforce project structure:
- `DependencyTests.cs` — project reference constraints
- `LayerTests.cs` — NetArchTest type-level rules
- `DiRegistrationTests.cs` — DI registration completeness
- `NamingConventionTests.cs` — class naming per layer (if added in the future)
- `ProjectTaxonomyTests.cs` — layer boundary enforcement (if added in the future)
- `ApiCompositionRootTests.cs` — Api stays thin (if added in the future)

**Rules for AI agents:**
1. NEVER add items to exclusion lists (`*Excluded` HashSets) to make your code compile
2. NEVER weaken assertions (changing `BeEmpty()` to `HaveCountLessThan()`)
3. NEVER delete or skip architecture tests
4. NEVER change `[InlineData]` dependency allowlists without user approval
5. If your code violates an architecture test, fix your code — not the test
6. If you believe a rule is wrong, TELL the user and wait for approval before changing

Commits that modify architecture test files MUST include `[arch-change-approved]` in the commit message.

## Orchestrator Design Rules

**Orchestrators are thin coordinators — they dispatch, they don't implement.**

- An orchestrator should call at most **5-6 Activities/Services directly**
- If an orchestrator has too many inline service calls, group related calls into an Activity
- Activities can call Services internally — the orchestrator doesn't need to know the details
- Example: instead of `DraftEmail → SendEmail → DraftNotification → SendNotification` as 4 separate orchestrator calls, group into `NotifyPartiesActivity` that handles all 4 internally

**Call hierarchy (Durable Functions):**
```
Orchestrator (Durable Function — checkpoint/replay)
  ├─ dispatches → Sub-Workers (Activity Function — pure compute)
  ├─ calls → Activities (Activity Function — compute + persist via DataServices)
  └─ calls → Services (Activity Function — sync business logic)
```

**Key constraints:**
- Workers: pure compute, NO storage, NO DataServices — call Clients only
- Services: CAN call Clients (Gmail, WhatsApp, Anthropic) + DataServices — CANNOT call Activities or Workers
- Activities: CAN call Services, launched by Orchestrators ONLY
- DataServices: storage routing (WHERE to store) — called by Activities and Services
- Data: raw I/O providers (HOW to store) — CAN call Clients (e.g., GDrive, Azure Blob) — called by DataServices only

**Orchestrator replay safety:**
- Changing activity dispatch order (e.g. parallel → sequential) breaks in-flight instances
- Always purge running/failed instances before deploying orchestrator code changes
- Never assume an orchestrator will only run against fresh history

**Memory budget (Azure Consumption plan = 1.5 GB):**
- NEVER run two memory-heavy activities in parallel (e.g. EmailFetch + DriveIndex)
- Workers that process external data (Drive, Gmail) must cap: max files, max file size, batch parallelism
- Download → process → release, never accumulate — page through data, don't load it all
- PDF/binary downloads: max 2 concurrent, release bytes immediately after Claude responds
- If a worker needs more data than fits in memory, split into multiple activity calls

## File Storage Abstraction

The `IFileStorageProvider` interface (defined in `RealEstateStar.Domain`) abstracts lead storage across Google Drive and local file system. Implementations live in `RealEstateStar.Data`:

- **Local** (`LocalStorageProvider` in `RealEstateStar.Data`): Development/testing and current production fallback
- **In-Memory** (`InMemoryFileProvider` in `RealEstateStar.Data`): Unit testing
- **Lead stores**: `LeadFileStore` and `LeadStore` in `RealEstateStar.DataServices` — use whatever `IFileStorageProvider` is injected
- **Google Drive** (`GDriveClient` in `RealEstateStar.Clients.GDrive`): Future production storage in agent's Drive folder

All lead files are markdown with YAML frontmatter. Frontmatter keys are validated against the Lead schema; user content goes in the markdown body.

## Lead Pipeline Architecture

The lead processing pipeline uses **Azure Durable Functions** for automatic checkpoint/replay. The orchestrator dispatches activities sequentially, with CMA + HomeSearch in parallel. Idempotency guards on email/WhatsApp sends prevent duplicate delivery on replay.

```
Form Submit → Turnstile → HMAC → API Endpoint
  │
  ├─ Dedup: check GetByEmailAsync → update existing or create new
  ├─ Save Lead Profile.md
  ├─ Record consent (CSV + compliance triple-write)
  └─ Enqueue → Azure Queue "lead-requests"
                  │
                  └─ StartLeadProcessingFunction [QueueTrigger]
                       → LeadOrchestratorFunction (Durable)
                            │
                            ├─ LoadAgentConfig (activity)
                            ├─ ScoreLead (activity)
                            ├─ CheckContentCache (activity — skip CMA/HS on cache hit)
                            ├─ CMA + HomeSearch (parallel activities, partial completion)
                            ├─ GeneratePdf (activity, if CMA succeeded)
                            ├─ DraftLeadEmail (activity)
                            ├─ SendLeadEmail (activity, idempotency guarded)
                            ├─ NotifyAgent (activity, idempotency guarded)
                            ├─ PersistLeadResults (activity)
                            └─ UpdateContentCache (activity)
```

**Lead status progression:** `Received → Enriched → EmailDrafted → Notified → Complete`
**Instance ID:** `lead-{agentId}-{leadId}` (deterministic, dedup on re-enqueue)
**Retry:** DF RetryPolicy (maxAttempts: 4, 30s backoff, 2x coefficient)

## CMA Pipeline Architecture

When a seller lead is submitted, the CMA pipeline fetches comparable sales data from RentCast and generates a professional PDF report.

**Comp Selection:** `RentCastCompSource` implements a tiered selection strategy targeting 5 comps with a 6-month recency preference. Recent sales (≤ 6 months old) are prioritized; older sales backfill up to the target count. Each comp is annotated with `IsRecent` to distinguish newer from older sales in Claude's analysis.

**Subject Enrichment:** `CmaProcessingWorker.EnrichSubjectAsync` fills missing property details (beds, baths, sqft) from RentCast's subject property response.

**PDF Generation:** `CmaPdfGenerator` renders a professionally branded PDF with enriched subject property data, tiered comp table (no Source column; Age column shows months since sale), agent branding, and contact info.

**PDF Download:** `DownloadCmaEndpoint` (GET `/accounts/{accountId}/agents/{agentId}/leads/{leadId}/cma/download`) streams the PDF from Azure Blob Storage. PDFs are stored automatically when the CMA pipeline completes.


## Multi-Language Architecture

Agent sites support English and Spanish. Language flows through two axes:

**Agent capability** (activation): Phase 2 DF activity functions extract per-language skills from the agent's actual Spanish emails/docs. `LocalizedSkills` dictionary flows through DF serialization DTOs (`VoiceExtractionOutput`, `PersonalityOutput`, `MarketingStyleOutput`, `BrandExtractionOutput`, `BrandVoiceOutput` in `ActivationDtos.cs`) and is persisted via `PersistProfileInput.LocalizedSkills`. `CheckActivationCompleteFunction` performs per-language completion checks — when `Languages` contains `"es"`, it also verifies `Voice Skill.es.md` and `Personality Skill.es.md` exist.

**Contact preference** (lead pipeline): `Lead.Locale` captured at form submission and flows through `LeadOrchestratorInput.Locale` into downstream DF activity DTOs (`DraftLeadEmailInput.Locale`, `GeneratePdfInput.Locale`, `NotifyAgentInput.Locale`, `PersistLeadResultsInput.Locale`). Email drafter loads `AgentContext.GetSkill("VoiceSkill", locale)` for per-language voice. CMA PDFs and email templates render localized content.

**Key conventions:**
- Per-language skill files: `{Skill Name}.{locale}.md` (e.g., `Voice Skill.es.md`)
- Locale codes: BCP 47 (`en`, `es`)
- `AgentContext.GetSkill(skillName, locale)` — falls back to English if locale version doesn't exist
- Language detection: `LanguageDetector.DetectLocale(text)` in `Domain/Shared/Services/`
- Observability: `RealEstateStar.Language` ActivitySource + Meter
- TCPA consent text stays English regardless of locale (legal requirement)
- Language features are fully integrated with DF orchestrators — no BackgroundService involvement

## Docker / Production Notes

- **Agent config files** (`config/accounts/`) live at repo root, outside Docker build context (`apps/api/`). CI copies them into the build context before `docker build`.
- **Program.cs** checks `/app/config/accounts` (Docker path) first, falls back to relative path for local dev.
- **Startup validation**: Never throw on missing optional config — use warnings. Throwing prevents the container from starting and blocks ALL functionality.
- **After deploy**: Always verify `latestReadyRevisionName == latestRevisionName` via `az containerapp show`.

## Docs

- Design: `docs/plans/2026-03-09-repo-restructure-design.md`
- API Restructure Design: `docs/superpowers/specs/2026-03-21-api-project-restructure-design.md`
- Lead Submission Design: `docs/superpowers/specs/2026-03-19-lead-submission-api-design.md`
- Durable Functions Migration: `docs/superpowers/specs/2026-03-31-azure-durable-functions-migration-plan.md`
- Durable Functions Task Plan: `docs/superpowers/specs/2026-04-01-azure-durable-functions-task-plan.md`
- Durable Functions Operations: `docs/superpowers/plans/2026-04-02-durable-functions-operations-guide.md`
- Activation MVP Redesign: `docs/superpowers/specs/2026-04-05-activation-mvp-redesign.md`
- Architecture Diagrams: `docs/architecture/README.md`
- Onboarding: `docs/onboarding.md`
- PM Skills: `docs/pm-skills-setup.md`
