---
name: lead-notes-dual-source
description: "Lead notes come from two places — Lead.Notes (top-level form field) AND SellerDetails.Notes/BuyerDetails.Notes — always check both"
user-invocable: false
origin: auto-extracted
---

# Lead Notes Dual Source

**Extracted:** 2026-03-29
**Context:** Notes field not reaching Claude in CMA analysis despite being filled in the form

## Problem
The lead form has a single "Notes" textarea. Depending on the form structure:
- Top-level `request.Notes` maps to `Lead.Notes`
- `request.SellerDetails.Notes` maps to `Lead.SellerDetails.Notes`

The CMA analyzer only checked `lead.SellerDetails?.Notes` — missing notes that came via the top-level field. Production logs confirmed: `HasSellerNotes: False, HasTopLevelNotes: True`.

## Solution
Always check both sources with fallback:
```csharp
var sellerNotes = lead.SellerDetails?.Notes ?? lead.Notes;
```

## When to Use
- Any code that reads notes from a Lead object
- Building prompts for Claude that should include seller/buyer notes
- Logging whether notes were received (log BOTH sources)
