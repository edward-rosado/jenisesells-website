<!--
Sync Impact Report
==================
- Version change: (unfilled template) → 1.0.0 (initial ratification)
- Modified principles: n/a (first ratification — all placeholders replaced)
- Added sections:
  - Core Principles (7): Simplicity First; Architecture Boundaries Are Law;
    Multi-Tenant by Default; Locale Is a First-Class Dimension;
    Tests and Gates Are Non-Negotiable; Observability and Resilience Are
    Requirements; Security and Compliance Cannot Be Deferred
  - Platform & Technology Constraints
  - Development Workflow & Quality Gates
  - Governance
- Removed sections: none
- Templates:
  - ✅ .specify/templates/plan-template.md — Constitution Check seeded with
    concrete per-principle gates
  - ✅ .specify/templates/tasks-template.md — test tasks changed from OPTIONAL
    to REQUIRED per Principle V
  - ✅ .specify/templates/spec-template.md — no changes required (user-story
    prioritization and independent-testability already align)
- Verification: every concrete claim (paths, class names, CI thresholds,
  workflow behaviors) was fact-checked against the repo by a 3-lens
  adversarial review (facts / contradictions / completeness) on 2026-07-16;
  live violations are recorded inline as KNOWN DEFECT / KNOWN GAPS entries.
- Follow-up TODOs (repo, not this document):
  - Reconcile the .NET version split: api.yml builds on 10 preview; deploys
    and csproj target net9.0 (pin via global.json)
  - Package-and-allowlist or remove packages/ui (unguarded, no package.json)
  - Backfill test projects for Clients.GooglePlaces and
    Workers.Activation.EmailTransactionExtraction
  - Reconcile project counts across CLAUDE.md ("22"/"21"),
    docs/architecture/README.md ("44+"), and reality (56 production projects)
-->


# Real Estate Star Constitution

Real Estate Star is a white-label, multi-tenant SaaS platform that automates
real estate agent workflows — branded agent microsites, instant CMA reports,
and lead capture/response. This constitution governs how every feature is
specified, planned, built, and shipped in this repository.

## Core Principles

### I. Simplicity First, Iterate Later

Every design MUST start from the simplest option that ships. Deferred work is
written down (spec, plan, or PRODUCT_PLAN.md), not speculatively built.

- Scope decisions in `docs/PRODUCT_PLAN.md` are binding: contract drafting is
  OUT of MVP scope; billing (Stripe) is the lowest-priority workstream and
  MUST NOT block core CMA, lead, or agent-site work.
- Any complexity beyond the simplest viable design — a new project, a new
  package, a new orchestration layer, a new external dependency — MUST be
  justified in the plan's Complexity Tracking table before implementation.
- Post-MVP items (MLS integration, CRM integrations, SMS, client signing
  portal) stay deferred until a documented scope decision promotes them.

Rationale: this is a focused product built in limited hours. Scope was
deliberately cut once; the constitution keeps it cut.

### II. Architecture Boundaries Are Law

Dependency boundaries are enforced by tooling, not convention. Code that
violates a boundary is wrong — the boundary is not.

- **Backend (.NET, `apps/api/`)**: `Domain` owns ALL interfaces and depends on
  nothing. `Data`, `Clients.*`, `DataServices`, `Notifications`, and
  `Workers.Shared` depend on Domain only. `Workers.*` depend on Domain +
  Workers.Shared. `Api` and `Functions` are the ONLY composition roots.
  Clients MUST NOT reference other Clients (sole exception: the four Google
  API clients → `Clients.GoogleOAuth`). No circular dependencies. Enforced by
  `apps/api/RealEstateStar.Tests/RealEstateStar.Architecture.Tests/`.
- **Frontend packages (`packages/`)**: `domain` depends on nothing; every
  other package (`api-client`, `forms`, `legal`, `analytics`) depends only on
  `@real-estate-star/domain`. Enforced by `scripts/validate-architecture.mjs`
  (CI) and `packages/domain/__tests__/architecture.test.ts` (Vitest). A new
  shared package MUST be added to BOTH allowlists and ship a `package.json`,
  or it is unguarded and MUST NOT be merged. KNOWN DEFECT (at ratification):
  `packages/ui` predates this rule — no `package.json`, no allowlist entry —
  and MUST be packaged-and-allowlisted or removed before it gains an importer.
- **Frontend features (`apps/platform/features/`,
  `apps/agent-site/features/`)**: features MUST NOT cross-import; only
  `features/shared/` (platform) and the documented agent-site graph
  (`templates/ → sections/` subsection barrels, everything → `config/`) are
  permitted. Enforced by ESLint `no-restricted-imports` at error level.
  Barrels use named re-exports only — never `export *`.
- **Architecture tests are immutable without explicit approval.** NEVER add
  to exclusion lists, weaken assertions, edit `[InlineData]` allowlists, or
  skip/delete an architecture test to make code compile. Fix the code. Any
  change under `RealEstateStar.Architecture.Tests/**` MUST carry
  `[arch-change-approved]` in a commit subject line — the `arch-guard.yml`
  CI job greps commit headlines, so the tag in a message body alone does not
  pass — AND requires the maintainer's explicit prior approval.

Rationale: the boundaries exist so a backend of dozens of isolated projects
(56 production projects at ratification) and a multi-app frontend stay
independently comprehensible. Self-guarding count assertions exist precisely
so erosion is loud, not silent.

### III. Multi-Tenant by Default — Never Hardcode Agent Data

Every agent (tenant) is defined by config, and only by config.

- All agent identity, branding, location, integrations, and compliance data
  MUST come from `config/accounts/{handle}/` (`account.json`, `content.json`,
  `legal/`). Skills reference config via `{agent.*}` variable syntax.
- No agent-specific value (name, phone, brokerage, colors, state, license
  number) may appear in source code, templates, or pipeline logic.
- New features MUST work for any tenant. Test against the reference tenant,
  but ship fully generic.
- Runtime branding uses inline `style={}` with CSS custom properties
  (`var(--color-primary)`) — this is the intentional white-label pattern, not
  a migration target for Tailwind or CSS Modules.
- Both target users — solo agents and brokerage teams — MUST be considered in
  any tenant-facing design.

Rationale: the product IS the ability to stamp out branded agent experiences
from config. One hardcoded value breaks the business model.

### IV. Locale Is a First-Class Dimension

Locale flows through every layer that produces, transforms, or delivers
content to a human — like `agentId` or `correlationId`, it is never bolted on.

- Every DTO that carries user-facing content MUST have a `Locale` property;
  every synthesis-result DTO MUST carry `LocalizedSkills` where per-language
  content is extracted. Enforced by reflection-based `LocaleTests.cs` in
  Architecture.Tests — CI rejects PRs that drop locale fields.
- Every service method that drafts user-facing text (emails, PDFs,
  notifications) MUST accept a locale or `AgentContext` parameter.
- Per-language skill files follow `{Skill Name}.{locale}.md` with BCP 47
  codes (`en`, `es`); `AgentContext.GetSkill(name, locale)` falls back to
  English when a locale version is missing.
- Agent voice is preserved per language — authentic catchphrases and cultural
  expressions, not generic translations.
- TCPA consent text stays English regardless of locale (legal requirement).

Rationale: our agents serve bilingual communities; a Dominican agent's
"¡Pa'lante!" is not interchangeable with a generic "¡Adelante!".

### V. Tests and Gates Are Non-Negotiable

If a gate exists, it passes — gates are never bypassed, weakened, or skipped
to get a merge through.

- **Backend**: every production project MUST have a matching
  `{Project}.Tests` project under `apps/api/RealEstateStar.Tests/` using the
  standard stack (xUnit + FluentAssertions + Moq). Shared fakes and in-memory
  doubles live in `RealEstateStar.TestUtilities`, never re-implemented per
  project. New test frameworks (NUnit, MSTest) MUST NOT be introduced. KNOWN
  GAPS (at ratification, to backfill — not precedent):
  `Clients.GooglePlaces` and `Workers.Activation.EmailTransactionExtraction`
  lack test projects.
- **Testing conventions with teeth**: every serialized DTO/store has
  roundtrip tests (render/serialize → parse → all fields preserved,
  including nulls); every non-idempotent send (email, WhatsApp, notification)
  is guarded by `IIdempotencyStore` and has a test proving a duplicate key is
  skipped; user-supplied content paths have sanitization/injection tests.
- **Frontend**: Vitest only, with coverage thresholds pinned at 100% for
  included files — CI (`test:coverage`) fails below threshold. Components
  with user interaction have behavioral tests.
- **CI gates MUST pass before merge**: architecture validation, lint
  (including feature-isolation rules), full test suites, build, agent-site
  worker bundle size (hard fail > 3 MB, warn > 2.5 MB), and API Docker build
  with `config/accounts` copied into context.
- The .NET build treats code-style violations (e.g. unused usings) as build
  errors via `Directory.Build.props` — this stays on.

Rationale: a solo-maintained production system survives on automation, not
vigilance. Every gate replaced a class of shipped bug.

### VI. Observability and Resilience Are Requirements, Not Features

Every feature ships with the instrumentation and failure-handling needed to
operate it — or it isn't done.

- **Observability**: every API call carries `X-Correlation-ID`
  (auto-injected by api-client). Every user-facing form/action emits
  lifecycle telemetry (`Viewed`, `Started`, `Submitted`, `Succeeded`,
  `Failed` — PascalCase, matching backend `FormEvent`). Frontend errors go
  through `reportError()` from `@real-estate-star/analytics`, never
  `console.error`. Backend features with 3+ endpoints get an ActivitySource,
  Meter counters, and structured logging with correlation IDs. No PII in
  span tags or log fields.
- **Durable Functions resilience**: every `CallActivityAsync` passes a retry
  policy (`ActivationRetryPolicies.*` / `LeadRetryPolicies.*`) — no bare
  calls. Every activity function wraps core logic in
  try/catch/log-with-unique-code/rethrow; no bare or silent catch blocks.
  Activities are classified FATAL (propagate — orchestration should fail) or
  BEST-EFFORT (log warning, continue degraded) at design time.
- **Replay safety**: changing activity dispatch order, parallelism, or
  inserting activities mid-orchestrator is a breaking change — purge
  in-flight instances before deploying. New activities go at the END.
- **Fail loud**: Google API clients throw on missing credentials — never
  return null/empty so a pipeline "succeeds" with garbage.
- **Memory budget**: Azure Consumption plan = 1.5 GB. Never run two
  memory-heavy activities in parallel; cap file counts/sizes; download →
  process → release; binary/PDF parallelism ≤ 2.
- **Orchestrators stay thin**: at most 5–6 direct Activity/Service calls;
  group related calls into a single Activity.

Rationale: the pipelines run unattended against flaky external APIs (Google,
RentCast, WhatsApp, Anthropic). Retries, idempotency, and correlation IDs are
what make 2 a.m. failures diagnosable at 9 a.m.

### VII. Security and Compliance Cannot Be Deferred

Security checklists are applied when the matching code is touched, not in a
later hardening pass.

- **Request integrity**: agent-site → API calls are HMAC-signed with
  constant-time comparison (`CryptographicOperations.FixedTimeEquals`) and
  timestamp windows; public forms are Turnstile-protected; every HMAC failure
  mode has a unique log code.
- **Rate limiting**: API endpoints are rate-limited by default; the Stripe
  webhook endpoint is the sole documented exclusion (so signature and
  idempotency handling are never throttled).
- **OAuth**: `state` is a single-use CSRF nonce; `postMessage` uses exact
  target origins; tokens are stored encrypted with TTL (DPAPI + ETag
  optimistic locking in Azure Table).
- **Payments**: confirmation is webhook-driven, never client-click-driven;
  prices are server-side (Stripe PriceId); webhook signatures verified and
  idempotent.
- **File I/O**: user-supplied path components are allowlist-validated with
  `Path.GetFullPath` + base-path checks; outbound fetches of user-supplied
  URLs are HTTPS-only, domain-allowlisted, and private-IP-blocked.
- **Data privacy**: no lead PII stored in the platform database — lead
  delivery routes through the agent's own email account. No PII in telemetry.
- **Compliance surfaces**: Equal Housing Opportunity notice on all pages,
  GDPR cookie consent for EU visitors, CMA disclaimer on reports, and agent
  license-number display — present and tested wherever applicable.

Rationale: the platform handles consumer contact data, agent OAuth grants,
and payments in a regulated industry. Each checklist item maps to a real
failure class.

## Platform & Technology Constraints

- **Hosting is Cloudflare, always**: Pages/Workers via OpenNext — never
  Vercel. DNS on Cloudflare. The domain is `real-estate-star.com`
  (hyphenated) — use `real-estate-star` in ALL resource names (DNS, Azure,
  Cloudflare, GitHub); never `realestatestar`.
- **Frontend**: Next.js (App Router) for the admin platform
  (`apps/platform`, served at platform.real-estate-star.com) and white-label
  agent sites (`apps/agent-site`, `{handle}.real-estate-star.com`).
  Agent-site worker bundle has a hard 3 MB Cloudflare limit. Templates load
  via dynamic imports — one chunk per template. Packages declare
  `"sideEffects": false`.
- **Backend**: .NET API of isolated projects on Azure Container Apps,
  proxied through Cloudflare; pipelines on Azure Durable Functions (Flex
  Consumption). Build, test, and deploy workflows MUST target the same .NET
  version — a CI/deploy version split is a defect, not a convenience.
  KNOWN DEFECT (at ratification): `api.yml` builds/tests on .NET 10 preview
  while the deploy workflows and every csproj target net9.0 — reconcile to a
  single pinned version (e.g. via `global.json`).
- **HTTP layer**: REPR vertical slices — `Features/{Feature}/{Operation}/`
  with `{Operation}Endpoint/Request/Response`, `IEndpoint` auto-registration
  (no manual routes in Program.cs), explicit `CancellationToken` on every
  handler (no `= default`).
- **Observability stack**: OpenTelemetry → Grafana Cloud (backend); Sentry
  (agent-site, lazy-loaded); Cloudflare Web Analytics (auto-injected).
  Analytics keys: platform = our GA4 env vars; agent-site = BYOK from
  `account.json`.
- **Generated code**: `packages/api-client/generated/types.ts` is
  auto-generated from the OpenAPI spec — never hand-edit.
- **Docker**: `config/accounts/` lives outside the API build context; CI
  copies it in before `docker build`. Startup never throws on missing
  OPTIONAL config — warn instead, so the container can start.

## Development Workflow & Quality Gates

- **Spec-driven development**: non-trivial features flow through spec-kit —
  `/speckit-specify` → (`/speckit-clarify` when ambiguous) → `/speckit-plan`
  → `/speckit-tasks` → (`/speckit-analyze`) → `/speckit-implement`. Feature
  artifacts live in `specs/{###-feature-name}/` (created by spec-kit per
  feature). This is the forward-going convention; pre-spec-kit designs remain
  where they are in `docs/superpowers/` and `docs/plans/`. Trivial fixes and
  chores may skip spec-kit but never skip CI gates.
- **Constitution Check**: every plan passes the Constitution Check gate
  (seeded in `plan-template.md`) before Phase 0 research and again after
  Phase 1 design. Violations are either redesigned away or justified in
  Complexity Tracking.
- **Branching & commits**: feature branches from `main`; conventional commits
  (`feat:`, `fix:`, `docs:`, `chore:`, `test:`, `refactor:`, `perf:`,
  `ci:`), atomic, with messages that explain *why*.
- **Pull requests**: link GitHub Issues where applicable; include a test
  plan; pass the no-hardcoded-agent-data check; all CI checks green.
- **Deploys**: deploy jobs run only from `main` and only after their test
  job passes. API deploys run two-phase health verification (liveness
  `/health/live`, readiness `/health/ready`) and roll back the Container App
  revision on failure. After any container deploy, verify
  `latestReadyRevisionName == latestRevisionName`.
- **Docs stay true**: `docs/pitch-decks/` `.md` and `.html` pairs are updated
  together. When reality diverges from `PRODUCT_PLAN.md` or a spec's Status
  header, reconcile the doc in the same change — stale docs are defects.

## Governance

- **Supremacy**: this constitution supersedes other practice documents where
  they conflict. `CLAUDE.md` and `.claude/rules/*` remain the operational
  detail layer and MUST be kept consistent with this document; a conflict is
  resolved by amending one or the other, never by ignoring either.
- **Amendments**: proposed via PR that states the change, its rationale, and
  its migration impact. The maintainer (Eddie Rosado) approves all
  amendments. Version bumps follow semantic versioning:
  - MAJOR — principle removed or redefined in a backward-incompatible way
  - MINOR — principle/section added or guidance materially expanded
  - PATCH — clarifications and wording that do not change meaning
- **Architecture-test coupling**: any amendment that changes what the
  architecture tests enforce MUST land together with the test change and the
  `[arch-change-approved]` commit tag.
- **Compliance review**: every `/speckit-plan` run re-checks this document
  via the Constitution Check; code review verifies adherence; AI agents
  working in this repo MUST treat these principles as hard constraints and
  surface (not silently resolve) any conflict they find.

**Version**: 1.0.0 | **Ratified**: 2026-07-16 | **Last Amended**: 2026-07-16
