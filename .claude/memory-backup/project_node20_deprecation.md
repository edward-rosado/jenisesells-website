---
name: Node.js 20 Actions Deprecation
description: GitHub Actions running on Node.js 20 will be forced to Node.js 24 starting June 2, 2026 — update action versions
type: project
---

GitHub Actions CI is warning that Node.js 20 actions are deprecated. The following actions need updating:
- `actions/checkout@v4`
- `actions/setup-node@v4`
- `actions/upload-artifact@v4`
- `cloudflare/wrangler-action@v3`

**Why:** Actions will be forced to run with Node.js 24 by default starting **June 2, 2026**. After that date, builds may break if action versions aren't updated.

**How to apply:** Next time we're editing `.github/workflows/`, update these actions to versions that support Node.js 24. Can also set `FORCE_JAVASCRIPT_ACTIONS_TO_NODE24=true` to test early.
