---
name: deploy-timing-and-testing
description: Production deploys take ~3-5 min after merge. Don't tell Eddie to test until deploy is confirmed complete.
type: feedback
---

After merging a PR to main, the agent-site deploy takes 3-5 minutes. Do NOT tell Eddie to test until the deploy shows `completed success`.

**Why:** Multiple times Eddie tested on production before the deploy finished and saw the old code, causing frustration and wasted time.

**How to apply:**
- After merge, always check `gh run list` for the deploy status before saying "go test"
- If deploy is `in_progress`, wait and check again — don't tell the user to test yet
- Preview deploys from PRs are built with the PR code, but old preview URLs (from previous PRs) serve OLD code
- Production deploys only trigger when the relevant paths change (e.g., `apps/agent-site/**` or `packages/**`)
