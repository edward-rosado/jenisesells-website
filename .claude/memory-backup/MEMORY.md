# Real-Estate-Star - Project Memory

## Project Overview
- **Purpose**: SaaS platform for small real estate agents to automate their workflow
- **First customer**: Jenise Buckalew (Eddie's mom), NJ REALTOR
- **Forked from**: jenisesellsnj-hash/jenisesells-website (prototype)
- **Repo**: edward-rosado/jenisesells-website

## Planned Features
1. Contract automation  2. Auto-replies  3. Website generation  4. CMA automation  5. Scheduling  6. DocuSign → MLS

## Current State (2026-03-20)
- CMA pipeline API (apps/api) — .NET 10, REPR pattern, QuestPDF, Claude API, SignalR, OTel
- Onboarding chat flow (Features/Onboarding) — profile scraping, state machine, tool dispatch
- **Lead Submission API** — MERGED to main (PR #28), 1182 tests
  - [project_lead_submission.md](project_lead_submission.md) — full feature details
- Platform (apps/platform) LIVE at https://platform.real-estate-star.com
  - Next.js 16, OpenNext → Cloudflare Workers, 230 tests, 100% coverage
- Agent site (apps/agent-site) — Cloudflare Workers, config registry prebuild
  - Subdomain routing: {handle}.real-estate-star.com
- WhatsApp agent channel on branch `feat/whatsapp-agent-channel-v2` — PR #30 open, all CI green
- API deploy pipeline exists (`deploy-api.yml`) — Azure Container Apps target

## Scripts
- **Coverage**: `bash apps/api/scripts/coverage.sh` — runs tests with XPlat Code Coverage, outputs per-class branch coverage summary. Use `--low-only` flag to show only classes below 100%.

## Tech Stack (Decided)
- Portal: Next.js 16 (Real Estate Star branded)
- Agent Sites: Next.js 16 (white-label)
- API: .NET 10
- PM: GitHub Issues + Projects (free)

## Key Architecture Decisions
- Agent profiles: JSON config files in config/agents/ (simple, no DB yet)
- Skills are multi-tenant: read from agent config, no hardcoded values
- Hosting: Cloudflare Pages (zero egress, unlimited BW, best edge perf)
- Credentials: ASP.NET Data Protection API (AES-256) → Azure Key Vault for key protection in prod

## Architecture Rules Are INVIOLABLE
- [feedback_never_break_architecture.md](feedback_never_break_architecture.md) — NEVER break dependency rules, no exceptions, no allowlists. Use Dependency Inversion instead.
- [feedback_arch_tests_belt_and_suspenders.md](feedback_arch_tests_belt_and_suspenders.md) — Always maintain BOTH reflection + NetArchTest suites

## Claude Setup
- Installed via edward-rosado/claude-setup into .claude/
- All rules, skills, and settings validated (--check passes)
- Uses junction-based symlinks (Windows, no Dev Mode)
- Permissions: all tools globally allowed in settings.local.json

## Tool Paths (Windows)
- **GitHub CLI**: `/c/Program Files/GitHub CLI/gh.exe` — added to PATH in `~/.bashrc` but Claude Code bash doesn't always source it. Run `source ~/.bashrc` at session start or prefix with `export PATH="/c/Program Files/GitHub CLI:$PATH" &&`
- **dotnet**: `/c/Program Files/dotnet/dotnet.exe` — usually in PATH
- Always check `~/.bashrc` and `/c/Program Files/` before assuming a tool is missing. Don't try to install tools — they're probably already there.

## User Preferences
- Eddie dislikes being prompted for git/mv/file operations — use `git -C` instead of `cd && git`
- Minimize confirmation prompts; trust the allowed permissions
- **100% branch coverage is REQUIRED** on all code. Never skip this. Run coverage after every implementation.
- CancellationToken must be REQUIRED (no `= default`) — this is a new project, not retrofitting
- **Never poll.** No sleep loops checking status. Fire the command, report the result, stop. Wait for the user to say when to check again. ("It's not polite to stare.")

## .NET API Architecture (Architecture of Choice)
- [reference_dotnet_api_architecture.md](reference_dotnet_api_architecture.md) — Multi-project architecture: Api, Domain, Data, Notifications, Workers.*, Clients.*
- [feedback_dotnet_architecture_of_choice.md](feedback_dotnet_architecture_of_choice.md) — **Eddie's architecture of choice for ALL future .NET APIs** — rationale and how to apply
- [feedback_arch_tests_belt_and_suspenders.md](feedback_arch_tests_belt_and_suspenders.md) — Always maintain BOTH reflection + NetArchTest suites; overlap is intentional, add to both when adding constraints
- Design spec: `docs/superpowers/specs/2026-03-21-api-project-restructure-design.md`
- **"Who answers what?"** separation: Domain (pure models) → Data (storage) → DataServices (orchestration) → Notifications (delivery) → Workers (pipelines) → Clients (external APIs) → Api (HTTP + DI)
- Max isolation: every non-Api project depends only on Domain (+ Workers.Shared for workers)
- 41 architecture tests: 31 reflection-based (DependencyTests.cs) + 10 NetArchTest type-level (LayerTests.cs)
- REPR pattern still applies within Api/Features/ for endpoints

## REPR Vertical Slice Rules (Api layer)
- `Features/{Feature}/{Operation}/` — folder name = operation name = type prefix
- Endpoint class name MUST match folder name: `GetStatus/` → `GetStatusEndpoint`
- HTTP request DTO ≠ domain model — endpoint maps via feature-level mapper
- AddEndpoints auto-discovers IEndpoint — no manual registration
- Static Handle methods, `internal` visibility for testability

## Critical Integration Rule
- **When adding auth/headers/contracts to API endpoints, ALWAYS update the frontend client in the same change.** SEC-1 added bearer token auth to the API but didn't update ChatWindow.tsx or onboard/page.tsx to send the token — broke the entire onboarding flow. API + frontend are a contract — never change one side without the other.

## Onboarding Chat Flow Rules (NEVER FORGET)
- **Auto-send profileUrl on mount**: When session is created with a `profileUrl`, ChatWindow MUST auto-send it as a silent message (no user bubble). The user should never have to re-enter the URL. This has been a recurring bug 3+ times.
- **Action messages are ALWAYS silent**: Card callbacks (OAuth, payment, etc.) send `[Action: ...]` to the API but NEVER show as a visible user message. Use `sendMessage(text, { silent: true })`.
- **Both rules need tests**: Write a test that verifies auto-send fires on mount with profileUrl, and a test that verifies action messages don't add user bubbles.

## Local Dev Ports (NEVER FORGET)
- **API**: runs on port **5135** (configured in launchSettings.json)
- **Platform (Next.js)**: runs on port **3000**
- **`NEXT_PUBLIC_API_URL`** must be `http://localhost:5135` — the fallback default in code is port 5000 which is WRONG
- **`Platform:BaseUrl`** in appsettings.Development.json must be `http://localhost:3000`
- Created `apps/platform/.env.local` with `NEXT_PUBLIC_API_URL=http://localhost:5135`
- **This has caused blank screen bugs 3+ times** — always verify ports match before debugging further

## Security Lessons
- [feedback_security_lessons.md](feedback_security_lessons.md) — CMA + Lead pipeline security patterns (prompt injection, PII hashing, HMAC, YAML injection, rate limiting, ForwardedHeaders, IDOR, Docker)

## Web Scraping & LLM Parsing
- JS-heavy sites embed data in `<script>` tags (JSON-LD, `__NEXT_DATA__`) — extract BEFORE stripping HTML
- ScraperAPI `render=true` for JS rendering; Zillow blocks direct requests (403)
- Strip markdown code fences from LLM JSON output before `JsonDocument.Parse()`
- `HttpUtility.HtmlDecode()` on content from script tags (Zillow encodes with `&quot;`)

## Streaming / SSE
- `StreamWriter.AutoFlush = true` breaks Kestrel — use manual `FlushAsync()`
- `Results.Stream()` swallows exceptions — always try-catch with unique `[PREFIX-NNN]` log codes
- C# cannot `yield return` inside try-catch — collect into local var first
- `EnsureSuccessStatusCode()` loses error body — read body before throwing

## .NET Convention Reminders
- `[JsonStringEnumConverter]` on all JSON-serialized enums
- Request objects for 3+ params; `internal` Handle methods; shared TestHelpers/TestData.cs
- Validate required config keys at startup (throw, don't fallback to empty string)

## Cloudflare Deployment (CRITICAL)
- [feedback_cloudflare_deploy.md](feedback_cloudflare_deploy.md) — OpenNext MUST build in Linux/Docker (not Windows), API token required (not OAuth), .env.local override trap, wrangler config requirements, debugging 500s

## CI/CD Pipeline Lessons
- [feedback_cicd_pipeline.md](feedback_cicd_pipeline.md) — ESLint ignoring generated code, React 19 lint rules, wrangler deploy, .NET locale bugs, Vitest vs Jest, useSyncExternalStore coverage
- Skill: `.claude/skills/ci-cd-pipeline/SKILL.md` — full workflow templates, common failures, required secrets

## Command Formatting
- [feedback_powershell_commands.md](feedback_powershell_commands.md) — ALWAYS give single-line commands, never multi-line with backticks

## Pipeline Benchmarks
- [project_pipeline_benchmarks.md](project_pipeline_benchmarks.md) — Build and deploy times tracked over time to measure improvements

## Pricing & Product Positioning
- [project_pricing_model.md](project_pricing_model.md) — 14-day free trial + $14.99/mo, "Your business, automated by AI" positioning, Coming Soon gate

## CI/CD Maintenance
- [project_node20_deprecation.md](project_node20_deprecation.md) — GitHub Actions Node.js 20 deprecation, update actions before June 2, 2026

## Platform Test File Scope
- [feedback_test_scope.md](feedback_test_scope.md) — Map of which test files cover which components/pages; MUST check all affected files when changing ANY page copy

## Git Commit Discipline
- Always `git add` ALL modified files — committing tests without source breaks CI
- Run `git status` before committing to verify staged vs unstaged

## Windows Git Path Workaround
- [feedback_windows_paths.md](feedback_windows_paths.md) — `git show ref:path` fails on Windows; use `git archive | tar` to temp dir + `cat` via Bash instead of Read tool

## Deploy Main Only
- [feedback_deploy_main_only.md](feedback_deploy_main_only.md) — Only main gets deployed to production, enforce with branch guards in all deploy scripts

## Diagrams in Design Specs
- [feedback_diagrams_in_specs.md](feedback_diagrams_in_specs.md) — Always include ASCII/text diagrams in design specs and documentation during brainstorming

## Edge Runtime Patterns
- [reference_edge_runtime_pattern.md](reference_edge_runtime_pattern.md) — Prebuild config bundling for Cloudflare Workers (no fs at runtime); generate .ts from JSON at build time, sync lookups at runtime

## Agent Site Design Matching
- [feedback_agent_site_design.md](feedback_agent_site_design.md) — Tailwind v4 font override (!important required), config registry auto-generation, inline styles pattern, production site reference details

## CI Config Registry Prebuild
- [feedback_ci_config_registry.md](feedback_ci_config_registry.md) — CI must generate config-registry.ts before vitest (npm prebuild only runs before next build)

## Lead Submission API
- [project_lead_submission.md](project_lead_submission.md) — Full feature: endpoints, services, security, compliance status
- Branch `feat/lead-submission-api`, 50 commits, 1071 tests, all security + legal remediations complete

## WhatsApp Integration
- [research_whatsapp_integration.md](research_whatsapp_integration.md) — WhatsApp Business API signup, approval, pricing, multi-tenant model
- Design spec: `docs/superpowers/specs/2026-03-19-whatsapp-agent-channel-design.md`

## Learned Skills
- [yaml-frontmatter-injection](~/.claude/skills/learned/yaml-frontmatter-injection.md) — Escape user input in YAML frontmatter to prevent key injection
- [nextjs-monorepo-env-scoping](~/.claude/skills/learned/nextjs-monorepo-env-scoping.md) — Next.js reads .env from its own app dir, not monorepo root
- [tailwind-v4-native-binding-ci](~/.claude/skills/learned/tailwind-v4-native-binding-ci.md) — Fix @tailwindcss/oxide native binding failures on cross-platform CI
- [content-driven-component-migration](.claude/skills/learned/content-driven-component-migration.md) — Migrating hardcoded UI to JSON config with typed fallbacks
- [agent-site-component-creation](.claude/skills/learned/agent-site-component-creation.md) — Checklist for new agent-site section components
- [v8-unreachable-defensive-branches](~/.claude/skills/learned/v8-unreachable-defensive-branches.md) — Remove defensive null checks creating unreachable V8 branches
- [cross-branch-model-migration-merge](~/.claude/skills/learned/cross-branch-model-migration-merge.md) — Systematic type mapping and bulk test rewrite for cross-branch domain model renames
- [channel-fanout-background-service](.claude/skills/learned/channel-fanout-background-service.md) — Channel<T> fan-out BackgroundService pattern with test strategies
- [safe-branch-cleanup-diff-apply](~/.claude/skills/learned/safe-branch-cleanup-diff-apply.md) — Clean up messy branches without force push using git diff | git apply

## Dependency Placement
- [feedback_dependency_placement.md](feedback_dependency_placement.md) — Never add deps to wrong app; agent-site has 3 MiB Cloudflare Worker limit

## Coverage Thresholds Are Sacred
- [feedback_never_lower_coverage.md](feedback_never_lower_coverage.md) — NEVER lower coverage thresholds; always write more tests instead

## Branch Protection
- [feedback_branch_protection.md](feedback_branch_protection.md) — Main is protected, always use PRs, check for existing open PRs first

## PR Ledger
- [project_pr_ledger.md](project_pr_ledger.md) — Open/recent PRs with status; check at session start, update when PRs are created/merged

## GH CLI Log Piping
- [feedback_gh_log_piping.md](feedback_gh_log_piping.md) — gh run view output can't be piped to grep/head on Windows; use without piping

## Parallel Agent Execution
- [feedback_max_parallelization.md](feedback_max_parallelization.md) — Design for max parallelization; 6-8 concurrent agents work well with the multi-project architecture

## Safe Git Operations
- [feedback_no_force_push.md](feedback_no_force_push.md) — Never force push; always use safest approach (new branch + new PR over rebase)

## GitHub Fork Setup
- [feedback_github_fork_prs.md](feedback_github_fork_prs.md) — Repo is a fork; use `--repo` on `gh pr` commands

## NEXT_PUBLIC Env Vars
- [feedback_nextjs_env_production.md](feedback_nextjs_env_production.md) — NEXT_PUBLIC_* must be in .env.production, not CI-only injection

## Pipeline Architecture
- [feedback_checkpoint_resume_pipeline.md](feedback_checkpoint_resume_pipeline.md) — All pipelines must checkpoint each step; retries resume, not restart; saves Claude tokens + API credits

## Production Deploy Lessons (2026-03-22)
- [feedback_docker_config_files.md](feedback_docker_config_files.md) — Config files at repo root must be copied into Docker build context
- [feedback_startup_validation_no_throw.md](feedback_startup_validation_no_throw.md) — Never throw on missing optional config at startup; warn instead
- [feedback_azure_container_revision_check.md](feedback_azure_container_revision_check.md) — Always verify latestReady == latest after API deploy
- [feedback_turnstile_csp_cors.md](feedback_turnstile_csp_cors.md) — Turnstile needs frame-src in CSP; /telemetry needs CORS for subdomains
- [feedback_api_frontend_payload_mapping.md](feedback_api_frontend_payload_mapping.md) — Frontend LeadFormData != API SubmitLeadRequest; always map
- [feedback_gws_cli_not_in_docker.md](feedback_gws_cli_not_in_docker.md) — gws CLI not in Docker; email notifications broken in prod

## Deploy & Preview Domain Rules
- [feedback_deploy_timing.md](feedback_deploy_timing.md) — Don't tell Eddie to test until deploy shows completed; takes 3-5 min
- [feedback_preview_vs_production.md](feedback_preview_vs_production.md) — *.workers.dev needs separate config in Turnstile (dashboard), Google Maps API key (dashboard), and CORS (code)

## Google Places Data API (CRITICAL — read before touching)
- [feedback_google_places_api.md](feedback_google_places_api.md) — Complete requirements: no loading=async, CSP needs places.googleapis.com, callback pattern, billing, state filtering, Turnstile/CORS for previews

## OTel / Grafana Cloud (CRITICAL)
- [feedback_otel_grafana_lessons.md](feedback_otel_grafana_lessons.md) — URI trailing slash bug, silent export failures, health check gate, headers space-splitting, Grafana architecture (OTel → Mimir → PromQL)
- Dashboard: `infra/grafana/real-estate-star-api-dashboard.json` — 54 panels, 8 rows, import via Grafana Cloud UI
- OtlpExportHealthCheck on /health/ready — deploy fails on Unhealthy (PR #63)

## Convention-Based DI
- [feedback_convention_based_di.md](feedback_convention_based_di.md) — DI should use assembly scanning loops, not manual per-service registration. Apply during lead pipeline redesign Phase 4 fix.

## TypeScript / Agent Site Convention Reminders
- Never use `Function` type — use `(...args: unknown[]) => void` or precise signature
- No JSX inside try/catch — load data in try, return JSX outside
- ESLint warnings = errors in CI; stale eslint-disable comments also fail lint
