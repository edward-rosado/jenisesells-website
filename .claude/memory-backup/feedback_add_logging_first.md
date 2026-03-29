---
name: add-logging-first
description: When debugging production issues, add diagnostic logging FIRST instead of guessing at fixes
type: feedback
---

When something isn't working in production, ADD DIAGNOSTIC LOGGING before attempting fixes.

**Why:** The PricingStrategy bug took 4 PRs to fix because we kept guessing. Adding `[CMA-ANALYZE-003]` logging immediately revealed both root causes (notes mapping + ReportType gate) in one test run.

**How to apply:**
1. When Eddie says "X isn't working" — add logging at every step of the data flow
2. Log: what went in, what came out, whether fields are null/present
3. Use `LogInformation` for summaries (visible in production), `LogDebug` for full payloads
4. Deploy the logging, ask Eddie to re-test, read the logs
5. THEN fix based on actual evidence, not assumptions
6. Keep the logging permanently — it's observability, not debug noise
