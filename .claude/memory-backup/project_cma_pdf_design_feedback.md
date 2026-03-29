---
name: cma-pdf-design-feedback
description: Eddie's feedback on CMA PDF layout — header, spacing, images, alignment issues to fix
type: project
---

CMA PDF layout needs polish. Feedback from 2026-03-29 production testing:

**Header issues:**
- Logo image not rendering (should be top-left)
- Headshot image missing (should be circular, top-right)
- Agent name + license + contact info should be right-aligned
- "Comparative Market Analysis" title should be centered
- Header is too tall for the amount of text — make it shorter
- Company letterhead should include brokerage address
- Text starts too close to header on page 2 — needs padding

**Proposed header layout:**
```
[Logo]                Comparative Market Analysis           [Headshot circle]
Green Light Realty    Jenise Buckalew                      REALTOR® | Lic# 0676823
123 Main St, NJ      (347) 393-5993                       jenisesellsnj@gmail.com
```

**Why:** The current layout works but looks generic. The header should match Jenise's existing brand template with logo + headshot bookending the CMA title.

**How to apply:** Refactor `CmaPdfGenerator.AddHeader` in `Activities.Pdf`. Use QuestPDF Row with 3 columns (logo | title | headshot). Load images from agent config (logo URL + headshot URL via `IImageResolver`).
