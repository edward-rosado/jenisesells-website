# Agent Site Cloudflare Deployment Design

## Goal

Deploy agent websites to `{slug}.real-estate-star.com` on Cloudflare Workers (free tier), with build-time config bundling and per-agent DNS setup via the Cloudflare API.

## Deployment Target: Cloudflare Workers (NOT Pages)

The agent-site deploys as a **Cloudflare Worker** via `wrangler deploy`, matching the platform
pattern. `wrangler.jsonc` has `"main": ".open-next/worker.js"` — this is a Workers project.

**This distinction matters for:**
- Custom domains: Workers use `PUT /workers/domains` API, NOT Pages `POST /pages/projects/{project}/domains`
- DNS: CNAME targets `{worker-name}.{account-subdomain}.workers.dev`, NOT `*.pages.dev`
- Rollback: `wrangler rollback` (Workers), NOT Pages rollback UI

## Approach: Bundle All Configs at Build Time

Agent JSON configs are bundled into the Next.js app at build time via a generated registry module. A single Cloudflare Workers deployment serves all agents. Middleware extracts the subdomain and looks up the bundled config.

**Why this approach:**
- 1 agent today, maybe 5-10 near term -- bundle bloat is not a concern
- Config changes rarely after onboarding
- No dependency on the API being live for static marketing pages
- Simplest CI/CD -- matches the platform deploy pattern
- Migration path to runtime fetch (Approach C) when needed at 50+ agents

**Alternatives considered:**
- One deploy per agent -- too complex for current scale, N builds in CI
- Fetch from API at runtime -- adds latency and API dependency for static content

---

## 1. Config Bundling

The current `config.ts` uses `fs.readFileSync` to load agent configs. Cloudflare Workers have no filesystem. A prebuild script generates a TypeScript registry that imports all configs as static JSON modules.

```
                         BUILD TIME
  +-------------------------------------------------------------+
  |                                                              |
  |  config/agents/                                              |
  |    jenise-buckalew/                                          |
  |      config.json  --+                                        |
  |      content.json --+    prebuild script                     |
  |    future-agent/    |    (generate-config-registry.mjs)      |
  |      config.json  --+         |                              |
  |      content.json --+         v                              |
  |                     +-----------------------+                |
  |                     | config-registry.ts    |  generated     |
  |                     | (auto-generated)      |  .gitignored   |
  |                     +-----------+-----------+                |
  |                                 |                            |
  |                                 v                            |
  |                     +-----------------------+                |
  |                     |   next build          |                |
  |                     |   (bundles registry   |                |
  |                     |    into worker.js)    |                |
  |                     +-----------+-----------+                |
  |                                 |                            |
  |                                 v                            |
  |                     +-----------------------+                |
  |                     |  .open-next/          |                |
  |                     |    worker.js          |  configs       |
  |                     |    assets/            |  baked in      |
  |                     +-----------------------+                |
  +-------------------------------------------------------------+

                         RUNTIME (Cloudflare Worker)
  +-------------------------------------------------------------+
  |                                                              |
  |  Request: jenise-buckalew.real-estate-star.com               |
  |       |                                                      |
  |       v                                                      |
  |  middleware.ts                                               |
  |    extract subdomain: "jenise-buckalew"                      |
  |    rewrite URL: ?agentId=jenise-buckalew                     |
  |       |                                                      |
  |       v                                                      |
  |  page.tsx                                                    |
  |    loadAgentConfig("jenise-buckalew")                        |
  |       |                                                      |
  |       v                                                      |
  |  config.ts (updated)                                         |
  |    lookup in bundled config-registry                         |
  |    (NO fs calls, NO network calls)                           |
  |       |                                                      |
  |       v                                                      |
  |  Render template with agent config + content                 |
  +-------------------------------------------------------------+
```

### Prebuild Script: `generate-config-registry.mjs`

- Scans `config/agents/` for BOTH config layouts:
  - Directory layout: `config/agents/{id}/config.json` (e.g. `jenise-buckalew/config.json`)
  - Flat layout: `config/agents/{id}.json` (e.g. `test-agent.json`)
  - Skip files matching `bad-*.json` or `*.schema.json` (test fixtures, schema)
- For each agent, generates imports for:
  - `config.json` (required)
  - `content.json` (optional — falls back to generated default content)
  - `legal/*.md` files (optional — privacy, terms, accessibility above/below markdown)
- Reads `identity.website` from each config to build the `customDomains` map
- Writes `apps/agent-site/lib/config-registry.ts` with typed exports
- The generated file is `.gitignored` -- it is a build artifact, not source code

### Config.ts Changes

- Remove ALL `fs`, `fs/promises`, `path` imports — none work on Cloudflare Workers
- Import `configs`, `contents`, `legalContent` from `./config-registry`
- `loadAgentConfig(id)` becomes a sync lookup: `configs[id] ?? throw "unknown agent"`
- `loadAgentContent(id, config)` becomes: `contents[id] ?? buildDefaultContent(config)`
- `loadLegalContent(id, page)` becomes: `legalContent[id]?.[page] ?? { above: undefined, below: undefined }`
- Keep all validation logic (`assertAgentConfig`)
- Keep content fallback generation (`buildDefaultContent`)
- Functions change from `async` to sync (no I/O, just object lookup)

---

## 2. DNS & Subdomain Routing (Free Tier)

Cloudflare free tier does not support wildcard custom domains on Pages/Workers projects. Each agent subdomain is added explicitly via the Cloudflare API.

```
                         DNS (Cloudflare -- Free Tier)
  +-------------------------------------------------------------+
  |                                                              |
  |  Per-agent custom domains (added by deploy script):          |
  |                                                              |
  |  jenise-buckalew.real-estate-star.com                        |
  |    Workers custom domain (PUT /workers/domains)              |
  |    CF auto-creates DNS + SSL cert                            |
  |                                                              |
  |  future-agent.real-estate-star.com                           |
  |    Workers custom domain (same API)                          |
  |                                                              |
  |  (Each domain added via Cloudflare API during onboarding)    |
  |                                                              |
  |  Existing records (unchanged):                               |
  |  platform.real-estate-star.com  -> platform Workers          |
  |  real-estate-star.com           -> platform (redirect)       |
  |  www.real-estate-star.com       -> platform (redirect)       |
  |                                                              |
  +-------------------------------------------------------------+

                         ONE-TIME SCRIPT: add-agent-domain.ps1
  +-------------------------------------------------------------+
  |                                                              |
  |  Input: agent slug (e.g. "jenise-buckalew")                  |
  |                                                              |
  |  Step 1: Validate slug exists in config/agents/              |
  |                                                              |
  |  Step 2: Add Workers custom domain                           |
  |    PUT /accounts/{account_id}/workers/domains                |
  |    hostname: {slug}.real-estate-star.com                     |
  |    service: real-estate-star-agents                          |
  |    zone_id: {zone_id}                                        |
  |    (CF auto-creates the DNS record + SSL cert)               |
  |                                                              |
  |  Step 3: Check domain status once                            |
  |    GET /accounts/{account_id}/workers/domains/{domain_id}    |
  |    Print status. If not active, tell user to check later.    |
  |    (NO polling loop -- "it's not polite to stare")           |
  |                                                              |
  |  Step 4: Print confirmation URL                              |
  |                                                              |
  +-------------------------------------------------------------+

                         REQUEST FLOW
  +-------------------------------------------------------------+
  |                                                              |
  |  Browser: jenise-buckalew.real-estate-star.com               |
  |       |                                                      |
  |       v                                                      |
  |  Cloudflare DNS (auto-created by Workers custom domain)      |
  |    Routes to Worker: real-estate-star-agents                 |
  |       |                                                      |
  |       v                                                      |
  |  middleware.ts (edge)                                        |
  |    extract subdomain: "jenise-buckalew"                      |
  |    check reserved list: no                                   |
  |    check config registry: exists                             |
  |    rewrite: ?agentId=jenise-buckalew                         |
  |       |                                                      |
  |       v                                                      |
  |  page.tsx renders agent site                                 |
  |                                                              |
  |  Unknown subdomain: "random.real-estate-star.com"            |
  |    -> configs["random"] does not exist -> 404                |
  |                                                              |
  +-------------------------------------------------------------+
```

### Routing.ts Fix

- `BASE_DOMAINS`: change `"realestatestar.com"` to `"real-estate-star.com"`
- Add `"platform"` to `RESERVED_SUBDOMAINS` list

### Middleware.ts Change

- When subdomain is extracted but agent config does not exist in registry, return 404
- Do NOT fall through to a default agent

### Custom Domain Support

Agents may have their own domain (e.g. `jenisesellsnj.com`) in addition to their
`{slug}.real-estate-star.com` subdomain. The agent config already has an `identity.website`
field. The `add-agent-domain.ps1` script supports adding custom domains alongside subdomains.

```
                         CUSTOM DOMAIN FLOW
  +-------------------------------------------------------------+
  |                                                              |
  |  Agent config:                                               |
  |    identity.website: "jenisesellsnj.com"                     |
  |                                                              |
  |  add-agent-domain.ps1 "jenise-buckalew"                      |
  |       |                                                      |
  |       +-- Step A: Add subdomain (always)                     |
  |       |   Workers custom domain:                             |
  |       |     jenise-buckalew.real-estate-star.com              |
  |       |     -> real-estate-star-agents Worker                |
  |       |                                                      |
  |       +-- Step B: Add custom domain (if identity.website     |
  |       |   exists AND domain DNS is on Cloudflare)            |
  |       |   Workers custom domain:                             |
  |       |     jenisesellsnj.com                                |
  |       |     -> real-estate-star-agents Worker                |
  |       |                                                      |
  |       +-- Step C: Add www redirect (if custom domain added)  |
  |           Workers custom domain:                             |
  |             www.jenisesellsnj.com                             |
  |             -> real-estate-star-agents Worker                |
  |           Middleware: www.jenisesellsnj.com -> 301 redirect  |
  |             to jenisesellsnj.com                             |
  |                                                              |
  |  Middleware handles ALL:                                     |
  |    jenise-buckalew.real-estate-star.com -> subdomain match   |
  |    jenisesellsnj.com -> full hostname match in registry      |
  |    www.jenisesellsnj.com -> 301 redirect to bare domain      |
  |                                                              |
  +-------------------------------------------------------------+

                         MIGRATION FROM NETLIFY
  +-------------------------------------------------------------+
  |                                                              |
  |  Current state:                                              |
  |    jenisesellsnj.com -> Netlify (upstream repo auto-deploy)  |
  |                                                              |
  |  Migration steps:                                            |
  |    1. Deploy agent-site to Cloudflare Workers first          |
  |    2. Verify jenise-buckalew.real-estate-star.com works      |
  |    3. Add jenisesellsnj.com as custom domain on CF project   |
  |    4. Update DNS: point jenisesellsnj.com to Cloudflare      |
  |       (if DNS is on Cloudflare, just change the CNAME)       |
  |       (if DNS is elsewhere, update NS or CNAME there)        |
  |    5. Verify jenisesellsnj.com loads the new agent-site      |
  |    6. Decommission Netlify project                           |
  |                                                              |
  |  Rollback: if anything breaks, flip DNS back to Netlify      |
  |                                                              |
  +-------------------------------------------------------------+
```

### Config Changes for Custom Domains

The config-registry prebuild script also generates a hostname-to-agent-id mapping:

```typescript
// In config-registry.ts (generated)
export const customDomains: Record<string, string> = {
  'jenisesellsnj.com': 'jenise-buckalew',
};
```

Middleware checks this map when the hostname does not match `*.real-estate-star.com`:
1. Extract hostname from request
2. If hostname matches `*.real-estate-star.com` -> extract subdomain as agent ID (existing logic)
3. Else if hostname is in `customDomains` map -> use mapped agent ID
4. Else -> 404

### Routing.ts Changes

- Add `resolveAgentFromCustomDomain(hostname)` — imports `customDomains` from config-registry, returns agent ID or null
- Add `isWwwCustomDomain(hostname)` — checks if `www.{domain}` is in `customDomains`, returns bare domain for redirect
- `middleware.ts` calls `resolveAgentFromCustomDomain` as fallback when subdomain extraction returns null
- `middleware.ts` calls `isWwwCustomDomain` first, returns 301 redirect if matched
- Export `getAgentIds()` function for middleware to check if extracted subdomain exists in registry

### Middleware.ts Changes (detailed)

Current middleware passes through to `NextResponse.next()` when no subdomain is found. New behavior:

1. **www redirect**: If hostname is `www.{customDomain}`, 301 redirect to `https://{customDomain}{path}`
2. **Subdomain match**: If `extractAgentId(hostname)` returns an ID, verify it exists in the registry. If not in registry -> 404
3. **Custom domain match**: If subdomain extraction returns null, try `resolveAgentFromCustomDomain(hostname)`. If found -> rewrite with agent ID
4. **No match**: Return 404 (do NOT fall through to default rendering)
5. **CSP on custom domains**: The CSP header must work for custom domains too — `frame-ancestors 'none'` and `connect-src` remain the same since the API URL comes from `NEXT_PUBLIC_API_URL` env var (not from the request hostname)

---

## 3. CI/CD Pipeline

```
                         CI/CD: deploy-agent-site.yml
  +-------------------------------------------------------------+
  |                                                              |
  |  Trigger: push to main                                       |
  |    paths: apps/agent-site/**, config/agents/**               |
  |                                                              |
  |  +-------------------------------------------------------+  |
  |  |  Job: test                                             |  |
  |  |    npm ci                                              |  |
  |  |    npm run lint                                        |  |
  |  |    npm run test:coverage                               |  |
  |  |    next build (smoke test)                             |  |
  |  +---------------------------+---------------------------+  |
  |                              | pass                         |
  |                              v                              |
  |  +-------------------------------------------------------+  |
  |  |  Job: deploy                                           |  |
  |  |                                                        |  |
  |  |  1. npm ci                                             |  |
  |  |                                                        |  |
  |  |  2. Run prebuild script                                |  |
  |  |     node scripts/generate-config-registry.mjs          |  |
  |  |     -> scans config/agents/*/config.json               |  |
  |  |     -> writes lib/config-registry.ts                   |  |
  |  |                                                        |  |
  |  |  3. Build Next.js                                      |  |
  |  |     NEXT_PUBLIC_API_URL=${{ secrets.API_URL }}          |  |
  |  |     npx next build                                     |  |
  |  |                                                        |  |
  |  |  4. Build for Cloudflare                               |  |
  |  |     npx opennextjs-cloudflare build                    |  |
  |  |                                                        |  |
  |  |  5. Deploy                                             |  |
  |  |     npx wrangler deploy                                |  |
  |  |                                                        |  |
  |  +-------------------------------------------------------+  |
  |                                                              |
  +-------------------------------------------------------------+

                         LOCAL DEPLOY: deploy-agent-site.ps1
  +-------------------------------------------------------------+
  |                                                              |
  |  Same build pipeline, runs locally via Docker                |
  |  (matches deploy-platform.ps1 pattern)                       |
  |                                                              |
  |  1. Branch guard: must be on main                            |
  |  2. Prerequisites: node, docker, CLOUDFLARE_API_TOKEN        |
  |  3. Generate config registry                                 |
  |  4. Docker build (Linux -- avoids Turbopack bracket issue)   |
  |  5. wrangler deploy                                          |
  |                                                              |
  +-------------------------------------------------------------+
```

### Workflow Changes

- Add prebuild step (`node scripts/generate-config-registry.mjs`) before `next build` in BOTH jobs:
  - **test job**: prebuild before lint/test/smoke-build (tests import from config-registry)
  - **deploy job**: prebuild before production build
- Deploy uses `wrangler deploy` (Workers) matching wrangler.jsonc config
- Set `NEXT_PUBLIC_API_URL` env var in both build steps

### Local Deploy Script

- New file: `infra/cloudflare/deploy-agent-site.ps1`
- Follows same structure as `deploy-platform.ps1` (Docker build, prerequisites, branch guard)
- Runs prebuild script before the Docker build step
- Sets `NEXT_PUBLIC_API_URL` env var during build (from `.env.local` or parameter)
- **WARNING**: Do NOT commit `.env.local` with `NEXT_PUBLIC_API_URL` — it overrides CI secrets silently. The local deploy script reads it but `.gitignore` must include it

---

## 4. File Changes

### Modified

| File | Change |
|------|--------|
| `apps/agent-site/lib/config.ts` | Remove ALL fs/path imports. Replace with sync lookups from config-registry. Change async functions to sync. Add `loadLegalContent` registry lookup. |
| `apps/agent-site/lib/routing.ts` | Fix `BASE_DOMAINS` to `real-estate-star.com`. Add `platform` to `RESERVED_SUBDOMAINS`. Add `resolveAgentFromCustomDomain()`, `isWwwCustomDomain()`, `getAgentIds()`. Import `customDomains` from config-registry. |
| `apps/agent-site/middleware.ts` | Return 404 for unknown agents. Add custom domain fallback. Add www->bare 301 redirect. Verify subdomain exists in registry before rewriting. |
| `apps/agent-site/wrangler.jsonc` | Add `services` array with `WORKER_SELF_REFERENCE` binding pointing to `real-estate-star-agents` (matches platform pattern) |
| `apps/agent-site/package.json` | Add `prebuild` script: `node scripts/generate-config-registry.mjs` |
| `.github/workflows/deploy-agent-site.yml` | Add prebuild step in BOTH test and deploy jobs. Set `NEXT_PUBLIC_API_URL` env var. |
| `.gitignore` | Add `apps/agent-site/lib/config-registry.ts` |

### New

| File | Purpose |
|------|---------|
| `apps/agent-site/scripts/generate-config-registry.mjs` | Scans agent configs, generates registry |
| `infra/cloudflare/deploy-agent-site.ps1` | Local deploy script (Docker + wrangler) |
| `infra/cloudflare/add-agent-domain.ps1` | One-time: create CNAME + custom domain per agent |

### Updated Tests

| File | Change |
|------|--------|
| `apps/agent-site/lib/__tests__/config.test.ts` | Mock `config-registry` module instead of `fs`. Test sync behavior. Test `loadLegalContent` registry lookup. Test missing agent returns error. |
| `apps/agent-site/lib/__tests__/routing.test.ts` | Update domain to `real-estate-star.com`. Test `platform` is reserved. Test `resolveAgentFromCustomDomain`. Test `isWwwCustomDomain`. Test `getAgentIds`. |
| `apps/agent-site/__tests__/middleware/middleware.test.ts` | Test 404 for unknown subdomain. Test custom domain rewrite. Test www redirect. Test known subdomain rewrite. |

**Test fixtures**: Tests must NOT use `bad-*.json` files from `config/agents/` (those are schema validation test fixtures). Create test-specific mocks of the config-registry module.

### Not Changed

- `apps/agent-site/app/page.tsx` -- no changes needed (but callers of `loadAgentConfig` change from `await` to sync — page.tsx uses `await` so it must be updated to remove the `await`)
- `apps/agent-site/templates/*` -- rendering untouched
- `apps/agent-site/components/*` -- UI untouched
- `config/agents/**` -- source configs untouched

### Potentially Changed

| File | Change |
|------|--------|
| `apps/agent-site/app/page.tsx` | Remove `await` from `loadAgentConfig()` and `loadAgentContent()` calls (they become sync). Remove `await` from `loadLegalContent()`. |
| `apps/agent-site/app/[slug]/privacy/page.tsx` (if exists) | Same — remove `await` from `loadLegalContent()` |
| `apps/agent-site/app/[slug]/terms/page.tsx` (if exists) | Same |
| `apps/agent-site/open-next.config.ts` | May need `edgeExternals: ["node:crypto"]` if `crypto.randomUUID()` in middleware causes build issues on Cloudflare (test during build — only add if needed) |

---

## 5. First Deploy Sequence

For the initial deployment of Jenise's agent site:

```
  1. Implement config bundling (prebuild + config.ts changes)
  2. Fix routing.ts domain name + custom domain support
  3. Update middleware for 404 on unknown agents + custom domain fallback
  4. Update wrangler.jsonc
  5. Update CI workflow
  6. Create local deploy script + add-agent-domain script
  7. Run tests -- verify all pass
  8. Merge to main -- CI deploys to Cloudflare
  9. Run add-agent-domain.ps1 "jenise-buckalew"
     -> creates CNAME for jenise-buckalew.real-estate-star.com
  10. Verify: https://jenise-buckalew.real-estate-star.com loads
  11. Add jenisesellsnj.com as custom domain on CF project
  12. Update jenisesellsnj.com DNS to point to Cloudflare
  13. Verify: https://jenisesellsnj.com loads the agent-site
  14. Decommission Netlify project (prismatic-naiad-5466af)
```

### Rollback Plan

If the Cloudflare deployment fails or the agent site is broken:

1. **Worker rollback**: `wrangler rollback` reverts to the previous Worker version
2. **Custom domain rollback**: DNS for `jenisesellsnj.com` is only updated AFTER verification on the subdomain. If the subdomain doesn't work, custom domain migration hasn't started yet.
3. **Full rollback**: If `jenisesellsnj.com` DNS was already cut to Cloudflare and it breaks, point DNS back to Netlify (the Netlify project stays alive until step 14 of the deploy sequence)
4. **Netlify decommission is the LAST step** — never delete Netlify until both subdomain and custom domain are verified working

---

## 6. Future Migration Path

When agent count exceeds ~50 or self-service config editing is needed:

1. Add a `GET /agents/{id}/config` endpoint to the .NET API
2. Change `config.ts` to `fetch()` from the API instead of registry lookup
3. Add Cloudflare Cache API or stale-while-revalidate caching (60s TTL)
4. Remove prebuild script and config-registry
5. No changes to middleware, routing, DNS, or templates
