---
name: feedback_dependency_placement
description: Never add npm dependencies to the wrong app in a monorepo — agent-site has a 3 MiB Cloudflare Worker limit
type: feedback
---

Never add npm dependencies to the wrong app's package.json in this monorepo.

**Why:** Grafana Faro SDK (`@grafana/faro-web-sdk` + `@grafana/faro-web-tracing`) was added to `apps/agent-site/package.json` instead of `apps/platform/package.json`. Even though the code was never imported in app routes, the dependency bloated the Cloudflare Worker bundle past the 3 MiB free tier limit, breaking all preview deploys for weeks.

**How to apply:**
- Before adding a dependency, verify which app actually needs it — check which `apps/*/` directory the importing code lives in
- `apps/agent-site` deploys to Cloudflare Workers with a **3 MiB free tier limit** — every dependency must justify its bundle weight
- `apps/platform` deploys to Cloudflare Pages (no worker size limit) — heavier deps like Faro/OTel belong here
- If a feature is platform-only (status page, admin), its deps go in `apps/platform/package.json`
- If a feature is agent-facing (lead forms, privacy pages), its deps go in `apps/agent-site/package.json`
- Run `npm ls <package>` to verify a dep is only installed where expected
