# Demo Artifacts

Exhibits for the 2026-05-20 pitch. These are **real outputs** the system generated when the backend was live — used as demo exhibits because the API backend is offline (mid-migration).

## Files

| File | What it is | Use in demo |
|---|---|---|
| `sample-cma-report.pdf` | A real Comparative Market Analysis the CMA pipeline generated (lead: Jose Tejas). Branded, with comparable sales. ~177 KB. | **Beat 3** — the centerpiece exhibit. Open in a real PDF viewer. |
| `sample-cma-report-2.pdf` | A second real CMA report, different subject property. ~105 KB. | Backup / second example if asked for another. |

## Still to capture (optional — improves Beat 2)

Template screenshots for the multi-template story. To capture, run the agent site locally (see the demo script's "local run" section) and screenshot 4–5 visually distinct templates:
- `emerald-classic` (Jenise's live template)
- `luxury-estate`
- `coastal-living`
- `urban-loft`
- `modern-minimal`

Save them here as `template-{name}.png`. If there's no time, Beat 2 can be done with the live local tabs instead of screenshots.

## Provenance

The CMA PDFs were produced by `RealEstateStar.Workers.Cma` + `CmaPdfGenerator` (QuestPDF) during prior pipeline runs. They are genuine system output, not mockups.
