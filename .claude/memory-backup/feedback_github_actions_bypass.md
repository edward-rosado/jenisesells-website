---
name: github-actions-bot-bypass
description: github-actions[bot] is allowed to push directly to main for api-client type generation
type: feedback
---

`github-actions[bot]` is added to the branch protection bypass list for `main`. This allows the `api-client` workflow to push regenerated TypeScript types directly to main after the API build completes.

**Why:** The `api-client.yml` workflow generates TypeScript types from the OpenAPI spec and commits them. Without bypass, `git push` fails on the protected branch.

**How to apply:** If branch protection is ever reconfigured, re-add `github-actions[bot]` to the bypass list. GitHub Settings → Branches → main → Edit → Allow specified actors to bypass.
