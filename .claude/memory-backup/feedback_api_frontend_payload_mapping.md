---
name: api-frontend-payload-mapping
description: Frontend LeadFormData shape doesn't match API SubmitLeadRequest — always map before sending
type: feedback
---

The frontend `LeadFormData` type (shared-types package) does NOT match the API `SubmitLeadRequest` shape. A mapping layer is required.

**Why:** Frontend sends `leadTypes: ["selling"]` (array), API expects `leadType: "Seller"` (enum string). Raw `JSON.stringify(formData)` caused 400 Bad Request. Wasted a deploy cycle debugging this.

**How to apply:**
- `submit-lead.ts` has a `toApiPayload()` function that maps frontend → API format
- Key mappings: `leadTypes[]` → `leadType` enum, array to single value with Both logic
- The `packages/api-client` workspace package exists but types aren't generated yet — once wired up, this will be caught at compile time
- When changing API request DTOs, ALWAYS check the frontend mapping in `toApiPayload()`
- This is the same lesson as the Critical Integration Rule in CLAUDE.md — API + frontend are a contract
