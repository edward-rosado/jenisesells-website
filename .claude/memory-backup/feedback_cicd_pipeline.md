---
name: cicd-pipeline-lessons
description: CI/CD pipeline gotchas — ESLint ignoring generated code, React 19 lint rules, wrangler deploy, .NET locale bugs, Vitest vs Jest, useSyncExternalStore coverage
type: feedback
---

## CI/CD Pipeline Lessons

### 1. ESLint lints generated build output unless explicitly ignored
- `.open-next/` (OpenNext), `coverage/` (test reports) contain generated JS that fails lint
- ESLint `globalIgnores` in `eslint.config.mjs` must include `.open-next/**` and `coverage/**`
- Symptom: lint fails with thousands of errors in minified files at line 288

### 2. React 19 `react-hooks/set-state-in-effect` is a new ERROR
- `setState()` inside `useEffect` now fails lint (not just a warning)
- Fix: use `useSyncExternalStore` for reading external state (localStorage, etc.)
- This caught `CookieConsentBanner.tsx` which used `setVisible(true)` inside `useEffect`

### 3. Use `wrangler deploy` not `pages deploy` for Workers
- Real Estate Star platform is a Cloudflare Worker (via OpenNext), NOT a Pages project
- `pages deploy` deploys to the wrong target or fails
- The correct wrangler-action command is just `deploy`

### 4. Don't run `npm run build` before `opennextjs-cloudflare build`
- OpenNext's build command runs `next build` internally
- Running it separately doubles build time for no benefit

### 5. CI runs on Linux — no Docker workaround needed
- The Docker build workaround in `deploy-platform.ps1` is only for Windows
- GitHub Actions ubuntu-latest handles Turbopack bracket filenames natively
- Don't add Docker steps to the CI workflow

### 6. .NET `ToString("C0")` produces `¤` on Linux without explicit CultureInfo
- Windows defaults to `en-US` culture, so `500000m.ToString("C0")` → `$500,000`
- Ubuntu CI runners use invariant locale, so `ToString("C0")` → `¤500,000`
- **Fix**: ALWAYS pass `CultureInfo.GetCultureInfo("en-US")` as second arg to `ToString("C0", ...)`
- This broke `OnboardingMappersTests.ToAgentContent_SoldHomesEnabled_WhenPresent` in CI
- Also fixed 4 instances in `CmaPdfGenerator.cs` that had the same bug but no test coverage yet

### 7. Keep health check tests in sync with constructor changes
- `ClaudeApiHealthCheck` constructor added `IConfiguration` parameter — tests must mock it
- Use `ConfigurationBuilder().AddInMemoryCollection(...)` to create test config
- When health checks are unregistered from `Program.cs`, remove assertions expecting them

### 8. Two separate API workflows exist
- `api.yml` — build + test only (runs on push AND PRs to main)
- `deploy-api.yml` — build + test + Docker build + Azure Container Apps deploy (push to main only)
- Both trigger on `apps/api/**` path changes
- Deploy job requires `AZURE_CREDENTIALS` secret and Azure infra (ACR, Container App)

### 9. Vitest (not Jest) for Next.js platform tests
- Platform uses Vitest — use `vi.spyOn()` not `jest.spyOn()`
- Run tests with `npx vitest run` (not `npx jest`)
- Coverage: `npx vitest run --coverage`

### 10. `useSyncExternalStore` SSR coverage requires `renderToString`
- `getServerSnapshot` (3rd arg) only runs during SSR — JSDOM never calls it
- **Fix**: Add a test using `renderToString` from `react-dom/server`
- Storage event tests need `act()` wrapper when dispatching events manually

### 11. Skill created at `.claude/skills/ci-cd-pipeline/SKILL.md`
- Contains full workflow templates, common failures, and required secrets

### 12. Deploy API workflow has NO `workflow_dispatch` trigger
- Cannot manually re-trigger via `gh workflow run` or GitHub UI
- Only triggers on push to `main` with changes in `apps/api/**`
- To test the deploy: merge feature branch to main (with API changes)
- The deploy job also has `if: github.ref == 'refs/heads/main'` guard

### 13. AZURE_CREDENTIALS secret format
- Must be service principal JSON: `{"clientId":"...","clientSecret":"...","subscriptionId":"...","tenantId":"..."}`
- Created via `az ad sp create-for-rbac --name "github-deploy" --role contributor --scopes /subscriptions/{sub-id}/resourceGroups/{rg-name} --sdk-auth`
- Used by `azure/login@v2` action with `creds:` parameter

### 14. `az acr build` blocked — use local docker build + push
- ACR Tasks is blocked on this subscription with `TasksOperationsNotAllowed` error
- This is NOT a tier issue — even Basic tier ACR gets blocked on certain Azure subscriptions
- **Fix applied 2026-03-13**: changed `deploy-api.yml` to `az acr login` + `docker build` + `docker push`
- This is faster for iterative development anyway (local layer caching on the runner)

### 15. Always stage ALL related source + test files together
- Committing test changes that reference new constructors/signatures without the source file causes CI build failures
- Example: committed `ClaudeApiHealthCheck` tests (2-arg constructor) but forgot `ClaudeApiHealthCheck.cs` source — CI got "does not contain a constructor that takes 2 arguments"
- Rule: `git diff --cached` before committing to verify both sides of any interface change are staged

### 16. Local scripts must match CI workflows — ALWAYS update both
- When adding/changing env vars or build steps in a CI workflow, the corresponding local script MUST be updated in the same change
- **Caught 2026-03-13**: `deploy-platform.yml` had `NEXT_PUBLIC_COMING_SOON=true` but `infra/cloudflare/deploy-platform.ps1` did not — local deploys would produce a different build than CI
- **Also caught**: `setup.ps1` still referenced `apps/portal` (renamed to `apps/platform` months ago) — new contributors would get a broken setup
- Scripts to keep in sync:
  - `deploy-platform.yml` <-> `infra/cloudflare/deploy-platform.ps1` (env vars, build commands)
  - `deploy-api.yml` <-> `infra/azure/deploy-api.ps1` (Docker build args, health checks)
  - Any new app <-> `setup.ps1` (app directory names)

### 17. Run coverage locally BEFORE pushing — never rely on CI to catch gaps
- CI enforces 100% branch/line/function/statement coverage on both agent-site and platform
- Every new file, new branch (`? :` ternary), or new hook MUST have tests exercising both paths
- **Pre-push checklist:**
  1. `cd apps/agent-site && npx vitest run --coverage` — check thresholds pass
  2. `cd apps/platform && npx vitest run --coverage` — check thresholds pass
  3. `cd packages/ui && npx vitest run --coverage` — if shared components changed
- **Common coverage gaps that break CI:**
  - Adding `usePathname()` to a shared component (Nav) — ALL transitive consumer test files need the mock updated
  - New layout files with metadata exports — need tests for render AND metadata assertions
  - New props with ternary defaults (e.g., `agentFirstName ? dynamicLabel : staticLabel`) — need tests for both truthy AND falsy paths
  - NJ-specific conditional content (e.g., `brokerage ? ... : ...`) — need test fixtures with AND without the optional field
- **Rule:** If you create or modify ANY .tsx file, run coverage for that app before pushing. Don't wait for CI.
