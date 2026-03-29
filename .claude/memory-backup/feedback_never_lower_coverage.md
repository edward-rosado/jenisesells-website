---
name: Never lower coverage thresholds
description: NEVER change test coverage threshold values in vitest/jest config — always write tests to meet them
type: feedback
---

Eddie explicitly said: "you should never change the testing coverage targets. dont try to convince yourself otherwise."

This means:
- **NEVER** lower `thresholds.branches`, `thresholds.functions`, `thresholds.lines`, or `thresholds.statements` in vitest.config.ts or any test config
- If coverage fails, write more tests to cover the uncovered branches/lines
- Don't rationalize lowering thresholds as "pragmatic" — it's off limits
- This applies to ALL apps in the monorepo (platform, agent-site, api)
