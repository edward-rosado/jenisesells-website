---
name: design-for-max-parallelization
description: Design multi-task implementations for maximum parallel agent execution — independent tasks on isolated projects enable concurrent subagents
type: feedback
---

Design implementations for maximum parallelization of subagent work.

**Why:** The 21-project .NET architecture with Domain-only dependencies makes most tasks independent. The form-path-hardening feature (24 tasks) was executed with 8 concurrent agents successfully. Eddie wants this pattern repeated.

**How to apply:**
- When writing plans, group tasks by dependency — independent tasks should be dispatched as parallel agents
- The multi-project architecture (Domain defines all interfaces, each project depends only on Domain) means most backend tasks can run concurrently
- Frontend tasks (agent-site, UI package) can run in parallel with backend tasks
- Use worktree isolation for the feature branch, then dispatch subagents within it
- Prefer 6-8 concurrent agents as the sweet spot (tested successfully)
- Each agent should commit its own work when possible to avoid merge conflicts
