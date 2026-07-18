# Azure Cost Reduction Migration

## Context

Azure Free Trial spending limit hit on 2026-04-12. This migration reduces monthly Azure costs from ~$42-78 to ~$15-30 by switching two resources.

## Changes

### 1. Docker Registry: ACR → GitHub Container Registry

**Before:** `realestatestar.azurecr.io` (Basic, $5/month)
**After:** `ghcr.io/edward-rosado/real-estate-star-api` (free)

**Workflow changes** (`.github/workflows/deploy-api.yml`):
- `docker login ghcr.io` replaces `az acr login`
- Image tag: `ghcr.io/edward-rosado/real-estate-star-api:<sha>`
- Container App configured to pull from GHCR via `az containerapp registry set`

**New GitHub secret required:**
- `GHCR_PAT` — Personal Access Token with `read:packages` + `write:packages` scope
- Create at: GitHub → Settings → Developer Settings → Fine-grained tokens
- Container Apps needs this to pull images at runtime (workflow `GITHUB_TOKEN` is ephemeral)

### 2. Functions: Flex Consumption → Y1 Consumption

**Before:** `real-estate-star-functions-plan` (FC1 Flex Consumption, $20-40/month)
**After:** `real-estate-star-functions-plan-y1` (Y1 Dynamic, ~$0-5/month)

**Trade-offs:**
- Cold start: 5-10 seconds on first invocation after idle (acceptable — activation is queue-triggered)
- No always-ready instances (Flex had 1 always-ready HTTP instance)
- Free tier: 1M executions/month + 400K GB-s compute included
- Max timeout: 10 minutes (same as current host.json setting)

**Bicep template:** `infra/functions/main-consumption.bicep`

## Migration Steps

### Prerequisites
1. Upgrade Azure subscription from Free Trial to Pay-As-You-Go
2. Create `GHCR_PAT` GitHub secret

### Step 1: Functions migration (zero downtime)
```bash
# Run from repo root — creates new v4 function app alongside v3
bash infra/functions/migrate-to-consumption.sh
```

This creates `real-estate-star-functions-v4` on Y1 Consumption alongside the existing v3. Both run simultaneously.

### Step 2: Verify new function app
```bash
# Check functions are discovered
az rest --method get --url "https://management.azure.com/subscriptions/<SUB_ID>/resourceGroups/real-estate-star-rg/providers/Microsoft.Web/sites/real-estate-star-functions-v4/functions?api-version=2023-12-01" --query "value | length(@)"

# Check health
curl https://real-estate-star-functions-v4.azurewebsites.net/api/health

# Test activation
MSG='{"AccountId":"glr","AgentId":"jenise","Email":"jenisesellsnj@gmail.com","Timestamp":"2026-04-12T00:00:00Z","Tier":"Mvp"}'
B64=$(echo -n "$MSG" | base64)
az storage message put --queue-name activation-requests --account-name realestatestarstore --auth-mode key --content "$B64"
```

### Step 3: Cut over workflows
Update these files to point to the new function app:
- `.github/workflows/deploy-functions.yml`: `FUNCTION_APP_NAME: real-estate-star-functions-v4`
- `.github/workflows/deploy-api.yml`: `AzureFunctions__HealthUrl` env var

### Step 4: Cut over Docker registry
Merge the `deploy-api.yml` changes from this branch. First deploy will push to GHCR and configure Container App to pull from it.

### Step 5: Clean up old resources
```bash
# Delete old Flex Consumption resources (only after verifying v4 works)
az functionapp delete --name real-estate-star-functions-v3 --resource-group real-estate-star-rg
az appservice plan delete --name real-estate-star-functions-plan --resource-group real-estate-star-rg

# Delete old ACR (only after verifying GHCR works)
az acr delete --name realestatestar --resource-group real-estate-star-rg

# Delete old v2 remnants
az resource delete --ids $(az monitor app-insights component show --app real-estate-star-functions-v2 --resource-group real-estate-star-rg --query id -o tsv) 2>/dev/null || true
```

## Expected Monthly Costs After Migration

| Resource | Before | After |
|----------|--------|-------|
| Container Apps (API) | $15-30 | $15-30 (unchanged) |
| Container Registry | $5 | **$0** (GHCR free) |
| Functions plan | $20-40 | **$0-5** (Y1 free tier) |
| Storage | $2-3 | $2-3 (unchanged) |
| Key Vault | $0.50-1 | $0.50-1 (unchanged) |
| App Insights | $0-2 | $0-2 (unchanged) |
| **Total** | **$42-78** | **$18-41** |

**Savings: ~$24-37/month (45-50% reduction)**
