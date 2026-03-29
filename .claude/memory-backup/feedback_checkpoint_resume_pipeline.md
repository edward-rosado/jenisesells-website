---
name: checkpoint-resume-pipeline
description: All background pipelines (lead, CMA, home search) must use checkpoint/resume — check for existing output files before re-running expensive steps
type: feedback
---

Every pipeline step must save its output before proceeding. On retry, check if the output file exists and skip the step if it does.

**Why:** Retries were re-running Claude enrichment and ScraperAPI calls, wasting tokens and API credits. Eddie hit 70% ScraperAPI usage on the first test. Saving state at each step means retries resume from where they left off.

**How to apply:**
- Before enrichment: check if `Research & Insights.md` exists → skip Claude + scraping
- Before drafting email: check if `Notification Draft.md` exists → skip email body generation
- Before sending email: read saved draft from disk instead of rebuilding
- Before CMA: check lead status ≥ CmaComplete → skip
- Before home search: check lead status ≥ SearchComplete → skip
- Update `LeadStatus` after each step completes
- This pattern applies to ALL pipelines (lead, CMA, home search) — not just leads
- `IFileStorageProvider` read should follow a fallback chain: GDrive → local (same as write chain)
