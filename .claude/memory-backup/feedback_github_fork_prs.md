---
name: feedback_github_fork_prs
description: GitHub repo is a fork — gh pr commands need --repo flag to target Eddie's repo, not upstream
type: feedback
---

# GitHub Fork PR Targeting

The repo is a fork:
- **origin**: `edward-rosado/jenisesells-website` (Eddie's repo — PRs go here)
- **upstream**: `jenisesellsnj-hash/jenisesells-website` (parent — prototype source)

## The Problem
`gh pr create` defaults to targeting the **upstream** (parent) repo. This fails with:
```
pull request create failed: GraphQL: The edward-rosado:branch has no history in common with jenisesellsnj-hash:main
```

## The Fix
Always use `--repo edward-rosado/jenisesells-website` on all `gh pr` commands:
```bash
gh pr create --repo edward-rosado/jenisesells-website --title "..." --body "..."
gh pr checks 13 --repo edward-rosado/jenisesells-website
gh pr merge 13 --repo edward-rosado/jenisesells-website
gh pr list --repo edward-rosado/jenisesells-website
```

This applies to ALL `gh` commands that operate on PRs, issues, or checks.
