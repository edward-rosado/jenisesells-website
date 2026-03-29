---
name: pipeline-benchmarks
description: CI/CD pipeline build and deploy times — track over time to measure improvements
type: project
---

## Pipeline Benchmarks

Track build/deploy times to measure improvements over time.

### API Pipeline (deploy-api.yml)

| Date | Commit | Test Job | Deploy Job | Total | Notes |
|------|--------|----------|------------|-------|-------|
| 2026-03-13 | 3be4c4b | 43s | FAILED (3s) | N/A | ACR Tasks blocked (TasksOperationsNotAllowed) |
| 2026-03-13 | 7178173 | 47s | 1m14s | 2m04s | First successful deploy! Local docker build + push |

### API CI-Only Pipeline (api.yml)

| Date | Commit | Build+Test | Notes |
|------|--------|------------|-------|
| 2026-03-13 | 3be4c4b | 54s | PR #8 gated build |
| 2026-03-13 | 1c3e375 | 1m12s | Previous commit |

### Platform Pipeline (deploy-platform.yml)

| Date | Commit | Test Job | Deploy Job | Total | Notes |
|------|--------|----------|------------|-------|-------|
| (no data yet) | | | | | |

### Agent Site Pipeline (PR #18, 2026-03-16, commit 108dc41)

| Workflow | Duration | Notes |
|----------|----------|-------|
| Agent Site — lint, test, coverage | 2m23s | |
| Preview Deploy | 1m59s | Runs after lint-test-build |
| Platform — lint, test, build | 1m24s | |
| API — build and test (skip) | 3s | Skipped (no API changes) |
| **Total (wall clock, parallel)** | **4m29s** | All 3 workflows in parallel |

#### Agent Site — lint, test, coverage (2m23s)

| Step | Duration |
|------|----------|
| npm ci | 42s |
| Test | 28s |
| Next.js build | 20s |
| OpenNext build | 29s |
| Lint | 5s |

#### Preview Deploy (1m59s)

| Step | Duration |
|------|----------|
| npm ci | 33s |
| Next.js build | 18s |
| OpenNext build | 27s |
| Wrangler deploy | 29s |

> **Optimization opportunity:** Preview Deploy duplicates npm ci + Next.js build + OpenNext build from the lint-test-build job (~1m18s wasted). Caching build artifacts between jobs would cut ~1m off total.

### Step-Level Breakdown (API deploy-api.yml, 2026-03-13, commit 7178173)

| Step | Duration |
|------|----------|
| **Test Job** | |
| Checkout | 1s |
| Setup .NET 10 | 12s |
| Restore | 8s |
| Build | 14s |
| Test | 5s |
| **Deploy Job** | |
| Checkout | 1s |
| Azure Login | 13s |
| Login to ACR | 4s |
| Docker Build | 23s |
| Docker Push | 7s |
| Container App Update | 19s |
| Health Check Verify | 2s |
