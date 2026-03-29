---
name: nextjs-env-production-not-ci-injection
description: NEXT_PUBLIC_* vars must be in .env.production for build-time inlining — CI env var injection alone does not work reliably with OpenNext/Cloudflare
type: feedback
---

Public env vars (`NEXT_PUBLIC_*`) must be set directly in `.env.production`, not injected via CI env vars alone.

**Why:** Next.js inlines `NEXT_PUBLIC_*` at build time in client components. When the value is only set as a CI environment variable (GitHub Actions `env:` block) but not in `.env.production`, it doesn't reliably get inlined — especially with OpenNext/Cloudflare Workers builds. The Turnstile site key was set via CI but never appeared in the client bundle, causing the widget to never render. This wasted significant debugging time across multiple deploy cycles.

**How to apply:**
- Always put `NEXT_PUBLIC_*` values directly in `apps/agent-site/.env.production` — they're public keys anyway (rendered in browser HTML)
- Reserve CI `env:` injection for server-only secrets (like `TURNSTILE_SECRET_KEY`) that go through `wrangler secret bulk`
- When debugging "value not available in client component", check `.env.production` first — don't assume CI env vars work
