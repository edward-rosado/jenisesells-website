---
name: cma-comp-quality
description: CMA comps must match subject property type — don't mix condos with single-family homes. Use RentCast subjectProperty to filter.
type: feedback
---

# CMA Comp Quality — Property Type Matching

The first production CMA mixed condos (634sqft, 2bd/1ba) with single-family homes (1,452sqft, 3bd/2ba), producing a valuation range too wide to be useful.

**Why:** The `MapComps` property type filter only triggered when `request.SqFt` was set. When the lead didn't specify sqft, no filtering happened.

**Fix:** Use `RentCastValuation.SubjectProperty.PropertyType` to filter comps — the API always returns the subject's property type even when the lead didn't specify it. Filter comps to match:
- Single Family subject → exclude Condo, Apartment, Multi-Family
- Condo subject → exclude Single Family, Multi-Family
- If subject type is unknown → keep all (current behavior)

**How to apply:** In `RentCastCompSource.MapComps`, check `subjectProperty.PropertyType` (not just `request.SqFt`) when deciding whether to filter by property type. Pass `subjectProperty` through to the mapping function.
