---
name: Deploy Main Only
description: Only main branch should ever be deployed to production — enforce with branch guards in all deploy scripts and CI workflows
type: feedback
---

# Deploy Main Only (2026-03-14)

## Rule
**Only `main` gets deployed to production. Period.**

- CI/CD workflows (`deploy-*.yml`) already gate on `push: branches: [main]`
- Local deploy scripts (`deploy-platform.ps1`, `deploy-api.ps1`) MUST have a main-branch guard at the top
- No `-Force` flag to bypass — if you need to test a deploy, use a preview/staging workflow

## What Happened
The live site showed $14.99/mo pricing while main still had $10/mo — meaning a feature branch had been deployed directly. This should never happen.

## Prevention
1. All deploy scripts check `git rev-parse --abbrev-ref HEAD` and exit if not `main`
2. PowerShell skill updated with "Deploy Scripts: Main-Branch Guard" section
3. Any new deploy script MUST include this guard before prerequisites
