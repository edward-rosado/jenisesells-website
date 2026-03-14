# Agent Site Cloudflare Deployment Design

## Goal

Deploy agent websites to `{slug}.real-estate-star.com` on Cloudflare Workers (free tier), with build-time config bundling and per-agent DNS setup via the Cloudflare API.

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

- Scans `config/agents/*/config.json` using `fs` + `glob`
- For each agent directory, generates an import for `config.json` and `content.json`
- Writes `apps/agent-site/lib/config-registry.ts` with typed exports
- The generated file is `.gitignored` -- it is a build artifact, not source code

### Config.ts Changes

- Remove `fs.readFileSync` and `path.resolve` imports
- Import `configs` and `contents` from `./config-registry`
- `loadAgentConfig(id)` becomes a lookup: `configs[id] ?? throw "unknown agent"`
- `loadAgentContent(id, config)` becomes: `contents[id] ?? generateDefaultContent(config)`
- Keep all validation logic (required fields check)
- Keep content fallback generation for agents without content.json

---

## 2. DNS & Subdomain Routing (Free Tier)

Cloudflare free tier does not support wildcard custom domains on Pages/Workers projects. Each agent subdomain is added explicitly via the Cloudflare API.

```
                         DNS (Cloudflare -- Free Tier)
  +-------------------------------------------------------------+
  |                                                              |
  |  Per-agent CNAME records (added by deploy script):           |
  |                                                              |
  |  jenise-buckalew.real-estate-star.com                        |
  |    CNAME -> real-estate-star-agents.pages.dev                |
  |                                                              |
  |  future-agent.real-estate-star.com                           |
  |    CNAME -> real-estate-star-agents.pages.dev                |
  |                                                              |
  |  (Each record added via Cloudflare API during onboarding)    |
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
  |  Step 2: Create DNS CNAME record                             |
  |    POST /zones/{zone_id}/dns_records                         |
  |    name: {slug}.real-estate-star.com                         |
  |    content: real-estate-star-agents.pages.dev                |
  |    proxied: true                                             |
  |                                                              |
  |  Step 3: Add custom domain to Pages project                  |
  |    POST /pages/projects/{project}/domains                    |
  |    name: {slug}.real-estate-star.com                         |
  |                                                              |
  |  Step 4: Poll until domain status = active                   |
  |                                                              |
  |  Step 5: Print confirmation URL                              |
  |                                                              |
  +-------------------------------------------------------------+

                         REQUEST FLOW
  +-------------------------------------------------------------+
  |                                                              |
  |  Browser: jenise-buckalew.real-estate-star.com               |
  |       |                                                      |
  |       v                                                      |
  |  Cloudflare DNS                                              |
  |    CNAME -> real-estate-star-agents.pages.dev                |
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
  |       |   CNAME: jenise-buckalew.real-estate-star.com        |
  |       |          -> real-estate-star-agents.pages.dev        |
  |       |   Custom domain on Pages project                     |
  |       |                                                      |
  |       +-- Step B: Add custom domain (if identity.website     |
  |           exists AND domain DNS is on Cloudflare)            |
  |           Custom domain: jenisesellsnj.com                   |
  |           on the same Pages project                          |
  |                                                              |
  |  Middleware handles BOTH:                                    |
  |    jenise-buckalew.real-estate-star.com -> subdomain match   |
  |    jenisesellsnj.com -> full hostname match in registry      |
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

- Add a `resolveAgentFromCustomDomain(hostname)` function
- `middleware.ts` calls this as a fallback when subdomain extraction returns null

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

- Add prebuild step (`node scripts/generate-config-registry.mjs`) before `next build`
- The test job also runs prebuild so tests can import from config-registry
- Deploy uses `wrangler deploy` (Workers) matching wrangler.jsonc config

### Local Deploy Script

- New file: `infra/cloudflare/deploy-agent-site.ps1`
- Follows same structure as `deploy-platform.ps1` (Docker build, prerequisites, branch guard)
- Runs prebuild script before the Docker build step

---

## 4. File Changes

### Modified

| File | Change |
|------|--------|
| `apps/agent-site/lib/config.ts` | Replace `fs.readFileSync` with config-registry lookup |
| `apps/agent-site/lib/routing.ts` | Fix domain to `real-estate-star.com`, add `platform` to reserved, add `resolveAgentFromCustomDomain()` |
| `apps/agent-site/middleware.ts` | Return 404 for unknown agents, custom domain fallback lookup |
| `apps/agent-site/wrangler.jsonc` | Add services self-reference binding |
| `apps/agent-site/package.json` | Add `prebuild` script |
| `.github/workflows/deploy-agent-site.yml` | Add prebuild step, update deploy command |
| `.gitignore` | Add `config-registry.ts` |

### New

| File | Purpose |
|------|---------|
| `apps/agent-site/scripts/generate-config-registry.mjs` | Scans agent configs, generates registry |
| `infra/cloudflare/deploy-agent-site.ps1` | Local deploy script (Docker + wrangler) |
| `infra/cloudflare/add-agent-domain.ps1` | One-time: create CNAME + custom domain per agent |

### Updated Tests

| File | Change |
|------|--------|
| `apps/agent-site/lib/__tests__/config.test.ts` | Mock config-registry instead of fs |
| `apps/agent-site/lib/__tests__/routing.test.ts` | Update domain expectations |
| `apps/agent-site/__tests__/middleware/middleware.test.ts` | Test 404 for unknown agents |

### Not Changed

- `apps/agent-site/app/page.tsx` -- no changes needed
- `apps/agent-site/templates/*` -- rendering untouched
- `apps/agent-site/components/*` -- UI untouched
- `config/agents/**` -- source configs untouched

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

---

## 6. Future Migration Path

When agent count exceeds ~50 or self-service config editing is needed:

1. Add a `GET /agents/{id}/config` endpoint to the .NET API
2. Change `config.ts` to `fetch()` from the API instead of registry lookup
3. Add Cloudflare Cache API or stale-while-revalidate caching (60s TTL)
4. Remove prebuild script and config-registry
5. No changes to middleware, routing, DNS, or templates
