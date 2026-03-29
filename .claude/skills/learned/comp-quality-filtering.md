---
name: comp-quality-filtering
description: "RentCast comps need aggressive filtering — dedup cross-zip, exclude subject self-sale if stale, IQR outlier removal, property type match"
user-invocable: false
origin: auto-extracted
---

# CMA Comp Quality Filtering

**Extracted:** 2026-03-29
**Context:** RentCast returns junk comps that make CMAs look unprofessional

## Problems Encountered
1. **Same property, different municipality**: "49 Middlesex Rd Unit B, Matawan 07747" and "49 Middlesex Rd Unit B, Old Bridge 08857" — same unit, different zip
2. **Subject property as a comp**: 308 Myrtle St at 0.0 miles with identical specs — RentCast returns the subject's own prior sale
3. **Commercial mixed with residential**: $275K for 5,000 sqft ($55/sqft) — clearly commercial, not a valid residential comp
4. **Price/sqft outliers**: $55/sqft when other comps are $294-$566/sqft

## Solution (CompAggregator)

1. **Two-pass dedup**: Pass 1 by street+zip, Pass 2 by street-only (catches cross-zip duplicates)
2. **Subject self-sale**: Keep if recent (< 6 months, useful for flippers), exclude if stale (> 6 months)
3. **Property type filtering**: Exclude comps with different property type than subject
4. **IQR outlier removal**: Compute Q1/Q3/IQR on price/sqft, exclude outside 1.5*IQR bounds
5. **Municipality suffix stripping**: Remove "Township", "Twp", "Borough", "Boro", "Village", "City" before comparison

## When to Use
- Adding new comp sources beyond RentCast
- Debugging CMA reports with suspicious comp data
- Writing tests for comp filtering (use real-world data from failed CMAs)
