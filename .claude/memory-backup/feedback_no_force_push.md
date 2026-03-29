---
name: feedback_no_force_push
description: Always use the safest git approach — never force push, prefer new branch + new PR over rebase
type: feedback
---

Always follow the safest approach for git operations. Never force push to any branch.

**Why:** User explicitly said "i dont want you to force push" and "i would like you to always follow the safest approach always" when asked about rebasing a messy feature branch.

**How to apply:**
- When a branch needs cleanup, create a new branch from base and use `git diff | git apply` (see learned skill: safe-branch-cleanup-diff-apply)
- Never use `git push --force` or `git push --force-with-lease` unless the user explicitly requests it in that specific moment
- Never use `git rebase` if it would require a force push afterward
- Prefer creating `-v2` branches and new PRs over rewriting history
- Close old PRs with a reference to the new one
