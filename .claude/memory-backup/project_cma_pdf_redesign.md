---
name: cma-pdf-redesign
description: Requirements for CMA PDF report redesign — branding, legal, visuals, data enrichment from RentCast
type: project
---

# CMA PDF Redesign Requirements (2026-03-26)

Eddie reviewed the first production CMA PDF and identified these improvements:

## Branding & Legal (must-have)
- Agent license number (from account.json `agent.compliance.licenseNumber`)
- Brokerage name (from `agent.brokerage.name`)
- Brokerage logo image (needs URL or file path in account config)
- Agent photo/headshot (needs URL or file path in account config)
- Remove "RentCast" as data source — don't expose the API provider name
- Use "Recent Comparable Sales" or "Market Data" instead of source attribution

## Data Enrichment
- When lead doesn't specify beds/baths/sqft, pull from RentCast's `subjectProperty` response
- The `/v1/avm/value` response includes `subjectProperty` with beds, baths, sqft — use those as defaults
- Beds and sqft should never show as null in the PDF
- **Comp recency — tiered selection (target: 5 comps):**
  1. First, take comps from the last 6 months
  2. If we have 5+ recent comps → use the top 5 (by correlation), discard the rest
  3. If we have fewer than 5 recent comps → backfill with older comps (by correlation) until we reach 5
  4. Older comps that backfill get annotated as "Older sale" so Claude weights them less
  5. If total available comps (all ages) < 5, use whatever we have — never pad or invent
  6. Implementation: `RentCastCompSource.MapComps` splits by recency, fills to 5, annotates age. `ClaudeCmaAnalyzer` system prompt: "Weight recent sales (< 6 months) more heavily than older sales."

## Visual Design
- More engaging layout — less text-heavy, clearer visual hierarchy
- Include property photos when available (RentCast may have photo URLs)
- Include agent headshot in the header/footer
- Include brokerage logo
- Better use of color, charts, or visual comparisons for comp data
- Clearer structure: Executive Summary → Valuation → Comps Table → Market Trends → Agent Contact

## Technical Notes
- PDF generator: QuestPDF (fluent C# API)
- Current file: `apps/api/RealEstateStar.Workers.Cma/CmaPdfGenerator.cs`
- Agent config: `config/accounts/{handle}/account.json`
- RentCast subject property data available in `RentCastValuation.SubjectProperty` (need to add this field)
- Images need to be accessible via URL or embedded in config
