---
name: startup-validation-warn-not-throw
description: Never throw on missing optional config at startup — it prevents the container from starting and blocks ALL functionality
type: feedback
---

Never throw `InvalidOperationException` at startup for config that can be gracefully degraded. Use a warning log instead.

**Why:** The HMAC startup validation threw when `Hmac:HmacSecret` wasn't configured in production. This crashed EVERY new container revision for days (revisions 0000011-0000013 all failed). The API was stuck on an old revision while we kept pushing code that never went live. The middleware already handled missing config gracefully (skipped auth), so the throw was redundant.

**How to apply:**
- Startup config checks should log warnings, not throw, unless the service literally cannot function
- The middleware/handler layer should enforce requirements — startup is too early
- If you must validate at startup, check `builder.Environment.IsProduction()` not `!IsDevelopment()` (staging exists too)
- Always check `az containerapp show --query latestReadyRevisionName` after deploy to verify the new revision is active
