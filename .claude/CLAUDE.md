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
    RealEstateStar.Workers.Shared/   # Pipeline base classes (WorkerBase, steps, channels)
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

## File Storage Abstraction

The `IFileStorageProvider` interface (defined in `RealEstateStar.Domain`) abstracts lead storage across Google Drive and local file system. Implementations live in `RealEstateStar.Data`:

- **Local** (`LocalStorageProvider` in `RealEstateStar.Data`): Development/testing and current production fallback
- **In-Memory** (`InMemoryFileProvider` in `RealEstateStar.Data`): Unit testing
- **Lead stores**: `LeadFileStore` and `LeadStore` in `RealEstateStar.DataServices` — use whatever `IFileStorageProvider` is injected
- **Google Drive** (`GDriveClient` in `RealEstateStar.Clients.GDrive`): Future production storage in agent's Drive folder

All lead files are markdown with YAML frontmatter. Frontmatter keys are validated against the Lead schema; user content goes in the markdown body.

## Lead Pipeline Architecture

The lead processing pipeline uses a **checkpoint/resume** pattern. Each step saves its output before proceeding. On retry, the worker checks if the output file exists and skips completed steps. This saves Claude tokens and ScraperAPI credits.

```
Form Submit → Turnstile → HMAC → API Endpoint
  │
  ├─ Dedup: check GetByEmailAsync → update existing or create new
  ├─ Save Lead Profile.md
  ├─ Record consent (CSV + compliance triple-write)
  └─ Enqueue → Background Worker
                  │
                  ├─ Step 1: Enrich (checkpoint: Research & Insights.md)
                  │   └─ Skip if file exists (saves Claude + ScraperAPI)
                  ├─ Step 2: Draft email (checkpoint: Notification Draft.md)
                  │   └─ Skip if file exists
                  ├─ Step 3: Send notification (retry 3x → dead letter)
                  ├─ Step 4: Dispatch CMA (sellers)
                  └─ Step 5: Dispatch Home Search (buyers)
```

**Lead status progression:** `Received → Enriched → EmailDrafted → Notified → Complete`

## CMA Pipeline Architecture

When a seller lead is submitted, the CMA pipeline fetches comparable sales data from RentCast and generates a professional PDF report.

**Comp Selection:** `RentCastCompSource` implements a tiered selection strategy targeting 5 comps with a 6-month recency preference. Recent sales (≤ 6 months old) are prioritized; older sales backfill up to the target count. Each comp is annotated with `IsRecent` to distinguish newer from older sales in Claude's analysis.

**Subject Enrichment:** `CmaProcessingWorker.EnrichSubjectAsync` fills missing property details (beds, baths, sqft) from RentCast's subject property response.

**PDF Generation:** `CmaPdfGenerator` renders a professionally branded PDF with enriched subject property data, tiered comp table (no Source column; Age column shows months since sale), agent branding, and contact info.

**PDF Download:** `DownloadCmaEndpoint` (GET `/accounts/{accountId}/agents/{agentId}/leads/{leadId}/cma/download`) streams the PDF from Azure Blob Storage. PDFs are stored automatically when the CMA pipeline completes.


## Docker / Production Notes

- **Agent config files** (`config/accounts/`) live at repo root, outside Docker build context (`apps/api/`). CI copies them into the build context before `docker build`.
- **Program.cs** checks `/app/config/accounts` (Docker path) first, falls back to relative path for local dev.
- **Startup validation**: Never throw on missing optional config — use warnings. Throwing prevents the container from starting and blocks ALL functionality.
- **After deploy**: Always verify `latestReadyRevisionName == latestRevisionName` via `az containerapp show`.

## Docs

- Design: `docs/plans/2026-03-09-repo-restructure-design.md`
- API Restructure Design: `docs/superpowers/specs/2026-03-21-api-project-restructure-design.md`
- Lead Submission Design: `docs/superpowers/specs/2026-03-19-lead-submission-api-design.md`
- Architecture Diagrams: `docs/architecture/README.md`
- Onboarding: `docs/onboarding.md`
- PM Skills: `docs/pm-skills-setup.md`
