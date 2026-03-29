---
name: verify-before-claiming
description: Eddie caught multiple times where agent work was claimed complete but didn't land on main — always verify with production logs
type: feedback
---

STOP claiming fixes are done without verifying they actually work in production.

**Why:** This session had multiple incidents where agents claimed to move files, fix bugs, or add features — but the changes didn't land on main. The code was on a branch, the PR merged, but the actual file moves/changes were missing. Eddie had to repeatedly point out that nothing changed.

**How to apply:**
1. After merging a fix, check production logs (not just CI) to confirm the fix is live
2. After moving files, `find` for both old AND new locations to confirm the move happened
3. After adding a new field, trace it end-to-end through ALL hops (model → worker result → orchestrator → PDF)
4. Don't trust agent reports — grep the actual codebase to verify
5. When Eddie says "it's still not working" — add diagnostic logging FIRST, don't keep guessing
