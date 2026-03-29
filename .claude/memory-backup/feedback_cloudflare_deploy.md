---
name: cloudflare-deploy-lessons
description: Critical deployment lessons for Cloudflare Pages/Workers — OpenNext, Docker build, API tokens, env overrides. Prevents costly recurring deployment failures.
type: feedback
---

## Cloudflare Pages Deployment (CRITICAL — READ BEFORE ANY DEPLOY)

### 1. OpenNext MUST build inside Linux (Docker), not native Windows
- OpenNext explicitly warns "not fully compatible with Windows"
- Turbopack (Next.js 16 default bundler) produces chunk files with `[brackets]` in filenames like `[root-of-the-server]__aa3583ab._.js`
- On Windows, the wrangler bundler silently fails to include these chunks
- Result: worker deploys successfully but returns 500 Internal Server Error at runtime (`ChunkLoadError: Failed to load chunk`)
- **Fix**: Run `npm install` and `npx opennextjs-cloudflare build` inside a Docker container (`node:22-slim`), then deploy from the host with wrangler
- Do NOT use `--webpack` flag (user rejected this — Turbopack is the default and should stay)
- Do NOT install WSL Ubuntu (user rejected this — only `docker-desktop` WSL distro exists)
- The deploy script at `infra/cloudflare/deploy-platform.ps1` handles this automatically via `docker run`

### 2. CLOUDFLARE_API_TOKEN is required (OAuth won't work)
- OpenNext's deploy command spawns wrangler in a non-interactive child process
- OAuth login tokens are session-scoped and don't pass to child processes
- `wrangler whoami` may pass with OAuth but `wrangler deploy` (called by OpenNext) fails with "non-interactive environment" error
- **Fix**: Create a Custom API Token at https://dash.cloudflare.com/profile/api-tokens with permissions:
  - Account > Workers Scripts > Edit
  - Account > Cloudflare Pages > Edit (if deploying Pages)
  - Account > Account Settings > Read
  - User > User Details > Read
  - User > Memberships > Read
- The last two User-level permissions are required by wrangler for `/memberships` and `/user` API calls — without them, `wrangler secret bulk` fails with code 10000 even though the token is valid
- Set via `$env:CLOUDFLARE_API_TOKEN` or permanently with `[Environment]::SetEnvironmentVariable('CLOUDFLARE_API_TOKEN', 'token', 'User')`
- The "Edit Cloudflare Workers" template alone may not have enough permissions (got 400/9106 error) — use Custom Token

### 3. .env.local overrides .env.production during build
- Next.js loads `.env.local` with highest priority, even during `next build`
- If `.env.local` has `NEXT_PUBLIC_API_URL=http://localhost:5135`, the production build bakes in localhost instead of the production API URL
- Result: deployed site tries to call localhost:5135 instead of api.real-estate-star.com
- **Fix**: The deploy script temporarily renames `.env.local` to `.env.local.bak` during build, then restores it after

### 4. wrangler.jsonc requires WORKER_SELF_REFERENCE binding
- OpenNext needs the worker to call itself for middleware routing and ISR revalidation
- Without the `services` binding, the worker may fail silently
- Required config:
  ```json
  "services": [{ "binding": "WORKER_SELF_REFERENCE", "service": "real-estate-star-platform" }]
  ```

### 5. open-next.config.ts must be complete
- Minimal config (just wrapper + converter) causes validation error
- Required fields: wrapper, converter, proxyExternalRequest, incrementalCache, tagCache, queue
- Must include edgeExternals and middleware section
- See `apps/platform/open-next.config.ts` for the full working config

### 6. Cloudflare infrastructure details
- Account ID: `7674efd9381763796f39ea67fe5e0505`
- Workers project: `real-estate-star-platform`
- Preview URL: `https://real-estate-star-platform.misteredr.workers.dev`
- Custom domain: `platform.real-estate-star.com`
- Production API URL: `https://api.real-estate-star.com`

### 7. Debugging Cloudflare Workers runtime errors
- The 500 response body just says "Internal Server Error" — no details
- Use `wrangler tail --format pretty` to see the actual exception
- Must have CLOUDFLARE_API_TOKEN set in the shell running tail
- `curl -D -` shows headers including `x-opennext: 1` confirming OpenNext handler reached
