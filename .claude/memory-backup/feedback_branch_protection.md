---
name: feedback_branch_protection
description: Main branch is protected — always create PRs, never try to merge directly
type: feedback
---

# Branch Protection

Main branch has branch protection enabled. Never try to merge directly — always create a PR. Use `--admin` flag on merge only after CI passes if other protection rules block (e.g., missing required reviewers).

## Proactive PR Ledger
- **At session start**: Check `project_pr_ledger.md` and run `gh pr list` to update status of any open PRs
- **When creating PRs**: Add to the ledger immediately
- **When merging PRs**: Move from Open to Recently Merged in the ledger
- **Before creating a new PR**: Always check if one already exists for the current branch
- This should happen automatically without the user asking
