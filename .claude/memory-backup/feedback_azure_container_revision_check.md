---
name: always-verify-container-revision-active
description: After API deploy, always check latestReadyRevisionName matches latestRevisionName — new revisions can silently fail
type: feedback
---

After deploying the API, always verify the new Container App revision is actually running.

**Why:** Multiple deploys (revisions 0000011-0000013) deployed successfully to ACR but the container never started due to a startup exception. The old revision kept serving traffic. We kept pushing code thinking it was live, but it wasn't. Hours wasted.

**How to apply:**
- After `az containerapp update`, run: `az containerapp show --name real-estate-star-api --resource-group real-estate-star-rg --query "{latestReady:properties.latestReadyRevisionName, latest:properties.latestRevisionName}"`
- If `latestReady` != `latest`, the new revision is failing. Check logs: `az containerapp logs show --name real-estate-star-api --resource-group real-estate-star-rg --tail 50`
- Common causes: missing env vars, startup exceptions, health check failures
- The deploy workflow reports "success" even when the revision fails — it only checks image push, not container health
