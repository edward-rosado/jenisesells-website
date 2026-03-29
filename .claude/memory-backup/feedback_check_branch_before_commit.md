---
name: check-branch-before-commit
description: ALWAYS run git branch --show-current before committing — never assume you're on the right branch after checkout or worktree operations
type: feedback
---

# Always Verify Branch Before Committing

ALWAYS run `git branch --show-current` before committing to verify you're on the expected branch. Never assume `git checkout main` succeeded — other branches, worktrees, or stashed state can leave you on the wrong branch.

**Why:** Committed a Dockerfile fix to `fix/cors-security-debug-v2` instead of `main` because `git checkout main` silently failed (worktree had main checked out). Had to undo and redo, wasting tokens and time.

**How to apply:**
- Before ANY commit: `git branch --show-current` and verify
- After `git checkout`: verify the switch actually happened
- When working across worktrees: the main repo and worktree share refs — `git checkout main` can fail if main is checked out in the worktree
