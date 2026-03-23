# Frontend Architecture Rules

## Package Dependencies

Every frontend package depends on at most 1 internal package (`domain`):

```
domain         → nothing
api-client     → domain
forms          → domain
legal          → domain
analytics      → domain
```

Enforced by:
- `scripts/validate-architecture.mjs` (CI step)
- `packages/domain/__tests__/architecture.test.ts` (test runner)
- ESLint `no-restricted-imports` (editor + lint)

## Feature Isolation

Both apps organize code into `features/` folders. Each feature:
- Has a barrel `index.ts` with named re-exports
- Has colocated `__tests__/` directory
- Cannot import from other features (except `shared/`)
- Can import from any `@real-estate-star/*` package

### Adding a New Feature

1. Create `features/{name}/` with `index.ts` and `__tests__/`
2. Add named re-exports to `index.ts`
3. Create thin `app/{name}/page.tsx` that imports from the feature
4. Add ESLint `no-restricted-imports` rule blocking other features
5. Add the feature to the Feature Scope Map in CLAUDE.md

## Import Rules

- Barrel exports: `export { X } from './X'` (named), NEVER `export * from`
- Agent-site templates: import from subsection barrels (`sections/heroes/`), NOT `sections/index.ts`
- `middleware.ts`: may import from `features/config/` and `features/shared/` only

## Bundle Optimization (Agent-Site)

Agent-site has a 3MB Cloudflare Worker limit:
- Templates: loaded via `next/dynamic` (one chunk per template)
- Turnstile: dynamic import, `ssr: false`
- Sentry: lazy import in catch blocks only
- All packages: `"sideEffects": false` in package.json

Bundle size checked in CI: warn at 2.5MB, fail at 3MB.

## Styling

Section components use inline `style={}` with CSS custom properties (`var(--color-primary)`) for runtime branding. This is intentional for white-label multi-tenant sites — NOT a migration target for CSS Modules or Tailwind.

## Observability

- Every user-facing action needs a telemetry event
- Use `reportError()` from `@real-estate-star/analytics`, not `console.error`
- Telemetry events: PascalCase (`Viewed`, `Started`, `Submitted`, `Succeeded`, `Failed`)
- Correlation IDs auto-injected on all API calls via `createApiClient()`
- Platform analytics: our GA4 keys from env var
- Agent-site analytics: BYOK from `account.json` config
