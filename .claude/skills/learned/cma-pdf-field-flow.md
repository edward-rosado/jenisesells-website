---
name: cma-pdf-field-flow
description: "New CmaAnalysis fields must flow through CmaWorkerResult AND Orchestrator reconstruction — 3 places to update"
user-invocable: false
origin: auto-extracted
---

# CMA PDF Field Flow — Three Places to Update

**Extracted:** 2026-03-29
**Context:** Adding a new field to the CMA analysis (like PricingStrategy) that needs to appear in the PDF

## Problem
Adding a field to `CmaAnalysis` is not enough. The field must survive three hops:
1. `CmaAnalysis` (parsed from Claude JSON) → `CmaWorkerResult` (flattened for TCS)
2. `CmaWorkerResult` (orchestrator receives via TCS) → reconstructed `CmaAnalysis` (for PdfActivity)
3. `CmaAnalysis` → PDF rendering (must not be gated behind a report type)

PricingStrategy was added to `CmaAnalysis` and Claude returned it, but it was missing from `CmaWorkerResult` — the field was silently dropped. Even after fixing that, the PDF didn't render it because it was gated behind `ReportType.Comprehensive`.

## Solution
When adding a new field to CMA analysis:

1. **Add to `CmaAnalysis`** (`Domain/Cma/Models/CmaAnalysis.cs`)
2. **Add to `CmaWorkerResult`** (`Domain/Leads/Models/WorkerResults.cs`) — this is the TCS transport
3. **Map in `CmaProcessingWorker`** — pass `analysis.NewField` when constructing `CmaWorkerResult`
4. **Map in `LeadOrchestrator.GeneratePdfAsync`** — pass `cmaResult.NewField` when reconstructing `CmaAnalysis`
5. **Parse in `ClaudeCmaAnalyzer.ParseResponse`** — use `GetStringPropertyCaseInsensitive` (Claude may return any casing)
6. **Render in `CmaPdfGenerator`** — do NOT gate behind report type unless explicitly required
7. **Add diagnostic log** — `[CMA-ANALYZE-003]` should log `NewField=True/False` to trace the field through the pipeline

## When to Use
- Adding any new field to the CMA analysis output
- Debugging why a field appears in Claude's response but not in the PDF
