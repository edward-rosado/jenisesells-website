---
name: never-commit-to-main-directly
description: NEVER commit or push directly to main — always work on feature branches and merge via PR
type: feedback
---

NEVER commit directly to main. Always work on a feature branch and merge via PR.

**Why:** During the security fixes PR, I accidentally committed test updates directly to main instead of the PR branch. This caused merge conflicts, a broken deploy on main, and significant confusion. Eddie had to intervene.

**How to apply:**
- Before EVERY `git commit`, verify the current branch: `git branch --show-current`
- If on `main`, STOP and checkout a feature branch first
- Never use `git push origin main` — only push feature branches
- If you accidentally commit to main, `git reset --soft HEAD~1` and stash before switching
- Cherry-pick operations are risky — prefer clean rebases on the PR branch
- When resolving conflicts, always rebase the PR branch on main, never the reverse
