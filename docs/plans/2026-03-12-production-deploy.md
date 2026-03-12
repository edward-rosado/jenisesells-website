# Production Deploy Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deploy Real Estate Star to production — API on Azure Container Apps (free tier), frontends on Cloudflare Pages, monitoring via Grafana Cloud, DNS on Cloudflare.

**Architecture:** The API deploys as a Docker container to Azure Container Apps with env vars for all secrets. Cloudflare Pages serves both frontends (platform + agent-site) with wildcard subdomains for agent sites. Grafana Cloud (free tier) receives OTLP telemetry from the API. All secrets live in GitHub Secrets and Azure env vars — never in code.

**Tech Stack:** Azure Container Apps, Cloudflare Pages, Grafana Cloud, GitHub Actions, Docker, OpenTelemetry

---

## Chunk 1: Production Config + Dockerfile

### Task 1: Create appsettings.Production.json

**Files:**
- Create: `apps/api/RealEstateStar.Api/appsettings.Production.json`

**Context:** The API reads config from `appsettings.{Environment}.json`. Production config should reference environment variables for all secrets — Azure Container Apps injects these at runtime. The `ASPNETCORE_ENVIRONMENT` env var must be set to `Production`.

- [ ] **Step 1: Create production appsettings**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Cors": {
    "AllowedOrigins": [
      "https://realestatestar.com",
      "https://www.realestatestar.com",
      "https://*.realestatestar.com"
    ]
  },
  "Platform": {
    "BaseUrl": "https://realestatestar.com"
  },
  "Google": {
    "RedirectUri": "https://api.realestatestar.com/oauth/google/callback"
  }
}
```

Note: `Anthropic:ApiKey`, `Stripe:SecretKey`, `Stripe:WebhookSecret`, `Stripe:PriceId`, `Google:ClientId`, `Google:ClientSecret`, `ScraperApi:ApiKey`, `Cloudflare:ApiToken`, `Cloudflare:AccountId`, `Attom:ApiKey` all come from environment variables via the `__` convention (e.g., `Anthropic__ApiKey`). .NET reads env vars automatically.

- [ ] **Step 2: Verify locally**

Run: `ASPNETCORE_ENVIRONMENT=Production dotnet build apps/api/RealEstateStar.Api/RealEstateStar.Api.csproj`
Expected: Build succeeds (startup will fail without env vars, that's OK)

- [ ] **Step 3: Commit**

```bash
git add apps/api/RealEstateStar.Api/appsettings.Production.json
git commit -m "chore: add production appsettings with env var config"
```

### Task 2: Harden Dockerfile for Production

**Files:**
- Modify: `apps/api/Dockerfile`

**Context:** Current Dockerfile works but needs: trimmed publish for smaller image, explicit `ASPNETCORE_ENVIRONMENT=Production`, and `ASPNETCORE_URLS` for the correct port.

- [ ] **Step 1: Update Dockerfile**

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY RealEstateStar.Api/RealEstateStar.Api.csproj RealEstateStar.Api/
RUN dotnet restore RealEstateStar.Api/RealEstateStar.Api.csproj
COPY . .
WORKDIR /src/RealEstateStar.Api
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

RUN addgroup --system --gid 1001 appgroup && \
    adduser --system --uid 1001 --ingroup appgroup appuser && \
    chown -R appuser:appgroup /app

USER appuser

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
  CMD wget -q --spider http://localhost:8080/health/live || exit 1

EXPOSE 8080
ENTRYPOINT ["dotnet", "RealEstateStar.Api.dll"]
```

- [ ] **Step 2: Test Docker build**

Run: `cd apps/api && docker build -t res-api-test .`
Expected: Build succeeds, image is ~200MB

- [ ] **Step 3: Commit**

```bash
git add apps/api/Dockerfile
git commit -m "chore: harden Dockerfile for production deploy"
```

---

## Chunk 2: Azure Container Apps Setup

### Task 3: Create Azure Infrastructure Script

**Files:**
- Create: `infra/azure/setup.sh`

**Context:** Azure Container Apps has a generous free tier (180K vCPU-seconds/month). We use Azure CLI (`az`) to create the resources. This is a one-time setup script — not run in CI.

- [ ] **Step 1: Create setup script**

```bash
#!/usr/bin/env bash
set -euo pipefail

# --- Configuration ---
RESOURCE_GROUP="real-estate-star-rg"
LOCATION="eastus"
ENVIRONMENT="real-estate-star-env"
APP_NAME="real-estate-star-api"
REGISTRY="realestatestaracr"

echo "=== Step 1: Create Resource Group ==="
az group create --name "$RESOURCE_GROUP" --location "$LOCATION"

echo "=== Step 2: Create Container Registry (Basic SKU — free for 100 days, then ~$5/mo) ==="
az acr create --resource-group "$RESOURCE_GROUP" --name "$REGISTRY" --sku Basic --admin-enabled true

echo "=== Step 3: Create Container Apps Environment ==="
az containerapp env create \
  --name "$ENVIRONMENT" \
  --resource-group "$RESOURCE_GROUP" \
  --location "$LOCATION"

echo "=== Step 4: Build and push initial image ==="
az acr build --registry "$REGISTRY" --image "$APP_NAME:latest" --file apps/api/Dockerfile apps/api/

echo "=== Step 5: Create Container App ==="
ACR_SERVER=$(az acr show --name "$REGISTRY" --query loginServer -o tsv)
ACR_USERNAME=$(az acr credential show --name "$REGISTRY" --query username -o tsv)
ACR_PASSWORD=$(az acr credential show --name "$REGISTRY" --query "passwords[0].value" -o tsv)

az containerapp create \
  --name "$APP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --environment "$ENVIRONMENT" \
  --image "$ACR_SERVER/$APP_NAME:latest" \
  --registry-server "$ACR_SERVER" \
  --registry-username "$ACR_USERNAME" \
  --registry-password "$ACR_PASSWORD" \
  --target-port 8080 \
  --ingress external \
  --min-replicas 0 \
  --max-replicas 2 \
  --cpu 0.25 \
  --memory 0.5Gi \
  --env-vars \
    "ASPNETCORE_ENVIRONMENT=Production" \
    "Anthropic__ApiKey=secretref:anthropic-api-key" \
    "Stripe__SecretKey=secretref:stripe-secret-key" \
    "Stripe__WebhookSecret=secretref:stripe-webhook-secret" \
    "Stripe__PriceId=prod_U7k7m92bbHfqHE" \
    "Google__ClientId=secretref:google-client-id" \
    "Google__ClientSecret=secretref:google-client-secret" \
    "Cloudflare__ApiToken=secretref:cloudflare-api-token" \
    "Cloudflare__AccountId=secretref:cloudflare-account-id" \
    "ScraperApi__ApiKey=secretref:scraper-api-key" \
    "Attom__ApiKey=secretref:attom-api-key"

echo "=== Step 6: Get app URL ==="
APP_URL=$(az containerapp show --name "$APP_NAME" --resource-group "$RESOURCE_GROUP" --query "properties.configuration.ingress.fqdn" -o tsv)
echo ""
echo "API deployed at: https://$APP_URL"
echo ""
echo "=== Next steps ==="
echo "1. Set secrets:  az containerapp secret set --name $APP_NAME --resource-group $RESOURCE_GROUP --secrets anthropic-api-key=YOUR_KEY ..."
echo "2. Configure custom domain: az containerapp hostname add --name $APP_NAME --resource-group $RESOURCE_GROUP --hostname api.realestatestar.com"
echo "3. Add CNAME record in Cloudflare: api -> $APP_URL"
echo "4. Add GitHub secrets: AZURE_CREDENTIALS (service principal JSON)"
```

- [ ] **Step 2: Commit**

```bash
chmod +x infra/azure/setup.sh
git add infra/azure/setup.sh
git commit -m "infra: add Azure Container Apps setup script"
```

### Task 4: Create Azure Secrets Script

**Files:**
- Create: `infra/azure/set-secrets.sh`

**Context:** Azure Container Apps uses secrets that are referenced by env vars. This script sets all required secrets. Run once after `setup.sh`, then again whenever you rotate keys.

- [ ] **Step 1: Create secrets script**

```bash
#!/usr/bin/env bash
set -euo pipefail

APP_NAME="real-estate-star-api"
RESOURCE_GROUP="real-estate-star-rg"

echo "This script sets Azure Container App secrets."
echo "You will be prompted for each value."
echo ""

read -sp "Anthropic API Key: " ANTHROPIC_KEY && echo
read -sp "Stripe Secret Key: " STRIPE_KEY && echo
read -sp "Stripe Webhook Secret: " STRIPE_WEBHOOK && echo
read -sp "Google Client ID: " GOOGLE_ID && echo
read -sp "Google Client Secret: " GOOGLE_SECRET && echo
read -sp "Cloudflare API Token: " CF_TOKEN && echo
read -sp "Cloudflare Account ID: " CF_ACCOUNT && echo
read -sp "ScraperAPI Key: " SCRAPER_KEY && echo
read -sp "Attom API Key: " ATTOM_KEY && echo

az containerapp secret set \
  --name "$APP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --secrets \
    "anthropic-api-key=$ANTHROPIC_KEY" \
    "stripe-secret-key=$STRIPE_KEY" \
    "stripe-webhook-secret=$STRIPE_WEBHOOK" \
    "google-client-id=$GOOGLE_ID" \
    "google-client-secret=$GOOGLE_SECRET" \
    "cloudflare-api-token=$CF_TOKEN" \
    "cloudflare-account-id=$CF_ACCOUNT" \
    "scraper-api-key=$SCRAPER_KEY" \
    "attom-api-key=$ATTOM_KEY"

echo ""
echo "Secrets set. Restart the app to pick up changes:"
echo "  az containerapp revision restart --name $APP_NAME --resource-group $RESOURCE_GROUP"
```

- [ ] **Step 2: Commit**

```bash
chmod +x infra/azure/set-secrets.sh
git add infra/azure/set-secrets.sh
git commit -m "infra: add Azure secrets configuration script"
```

---

## Chunk 3: GitHub Actions — API Deploy to Azure

### Task 5: Update deploy-api.yml for Azure Container Apps

**Files:**
- Modify: `.github/workflows/deploy-api.yml`

**Context:** The current workflow deploys to Railway. Replace the deploy job with Azure Container Apps deployment using `az containerapp update`. This uses OIDC federated credentials (no long-lived secrets).

- [ ] **Step 1: Update deploy workflow**

```yaml
name: Deploy API

on:
  push:
    branches: [main]
    paths:
      - 'apps/api/**'
      - '.github/workflows/deploy-api.yml'

jobs:
  test:
    name: "API — build and test"
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 10
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
          dotnet-quality: 'preview'

      - name: Restore
        run: dotnet restore apps/api/RealEstateStar.Api.sln

      - name: Build
        run: dotnet build apps/api/RealEstateStar.Api.sln --no-restore --configuration Release

      - name: Test
        run: >
          dotnet test apps/api/RealEstateStar.Api.sln
          --no-build
          --configuration Release
          --logger "console;verbosity=minimal"

  deploy:
    name: "Deploy to Azure Container Apps"
    needs: test
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'

    steps:
      - uses: actions/checkout@v4

      - name: Azure Login
        uses: azure/login@v2
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}

      - name: Build and push to ACR
        run: |
          az acr build \
            --registry realestatestaracr \
            --image real-estate-star-api:${{ github.sha }} \
            --image real-estate-star-api:latest \
            --file apps/api/Dockerfile \
            apps/api/

      - name: Deploy to Container App
        run: |
          az containerapp update \
            --name real-estate-star-api \
            --resource-group real-estate-star-rg \
            --image realestatestaracr.azurecr.io/real-estate-star-api:${{ github.sha }}

      - name: Verify deployment
        run: |
          APP_URL=$(az containerapp show \
            --name real-estate-star-api \
            --resource-group real-estate-star-rg \
            --query "properties.configuration.ingress.fqdn" -o tsv)
          echo "Deployed to: https://$APP_URL"
          sleep 10
          curl -sf "https://$APP_URL/health/live" || echo "Health check pending (cold start)"
```

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/deploy-api.yml
git commit -m "ci: switch API deploy from Railway to Azure Container Apps"
```

### Task 6: Create Azure Service Principal for GitHub Actions

**Files:**
- Create: `infra/azure/create-gh-credentials.sh`

**Context:** GitHub Actions needs an Azure service principal to authenticate. This creates one and outputs the JSON for the `AZURE_CREDENTIALS` GitHub secret.

- [ ] **Step 1: Create script**

```bash
#!/usr/bin/env bash
set -euo pipefail

SUBSCRIPTION_ID=$(az account show --query id -o tsv)
RESOURCE_GROUP="real-estate-star-rg"

echo "Creating service principal for GitHub Actions..."

SP_JSON=$(az ad sp create-for-rbac \
  --name "real-estate-star-github" \
  --role contributor \
  --scopes "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP" \
  --json-auth)

echo ""
echo "=== Add this as GitHub Secret: AZURE_CREDENTIALS ==="
echo "$SP_JSON"
echo ""
echo "Run: gh secret set AZURE_CREDENTIALS --body '$SP_JSON'"

# Also grant ACR push access
CLIENT_ID=$(echo "$SP_JSON" | python3 -c "import sys,json; print(json.load(sys.stdin)['clientId'])")
ACR_ID=$(az acr show --name realestatestaracr --query id -o tsv)
az role assignment create --assignee "$CLIENT_ID" --role AcrPush --scope "$ACR_ID"
echo "ACR push permission granted."
```

- [ ] **Step 2: Commit**

```bash
chmod +x infra/azure/create-gh-credentials.sh
git add infra/azure/create-gh-credentials.sh
git commit -m "infra: add GitHub Actions service principal script"
```

---

## Chunk 4: Monitoring with Grafana Cloud

### Task 7: Configure OTLP Export to Grafana Cloud

**Files:**
- Modify: `apps/api/RealEstateStar.Api/appsettings.Production.json`
- Modify: `apps/api/RealEstateStar.Api/Program.cs` (if needed)

**Context:** The API already has OpenTelemetry (ActivitySource, Meter, structured logging). Grafana Cloud offers a free tier: 10K metrics series, 50GB logs, 50GB traces/month. The OTLP endpoint and auth token go in env vars.

- [ ] **Step 1: Update production appsettings for Grafana OTLP**

Add to `appsettings.Production.json`:

```json
{
  "Otel": {
    "Endpoint": "",
    "ServiceName": "real-estate-star-api",
    "Headers": ""
  }
}
```

The `Otel__Endpoint` and `Otel__Headers` env vars will be set on Azure Container Apps:
- `Otel__Endpoint=https://otlp-gateway-prod-us-east-0.grafana.net/otlp`
- `Otel__Headers=Authorization=Basic <base64(instanceId:token)>`

- [ ] **Step 2: Verify OTLP config is used in Program.cs**

Read `apps/api/RealEstateStar.Api/Diagnostics/OpenTelemetryExtensions.cs` and verify it reads `Otel:Endpoint` from config. If it uses a hardcoded localhost, update it to read from config.

- [ ] **Step 3: Add Grafana setup instructions**

Create `infra/grafana/README.md`:

```markdown
# Grafana Cloud Monitoring Setup

## 1. Create Free Account
Go to https://grafana.com/auth/sign-up/create-user — free tier includes:
- 10K metrics series
- 50GB logs/month
- 50GB traces/month

## 2. Get OTLP Credentials
1. Navigate to: Grafana Cloud Portal → your stack → "OpenTelemetry"
2. Copy the OTLP endpoint URL (e.g., `https://otlp-gateway-prod-us-east-0.grafana.net/otlp`)
3. Generate an API token with `MetricsPublisher`, `LogsPublisher`, `TracesPublisher` roles
4. The auth header format: `Authorization=Basic <base64(instanceId:apiToken)>`

## 3. Set Environment Variables on Azure
```bash
az containerapp secret set \
  --name real-estate-star-api \
  --resource-group real-estate-star-rg \
  --secrets "otel-headers=Authorization=Basic YOUR_BASE64_CREDS"

az containerapp update \
  --name real-estate-star-api \
  --resource-group real-estate-star-rg \
  --set-env-vars \
    "Otel__Endpoint=https://otlp-gateway-prod-us-east-0.grafana.net/otlp" \
    "Otel__Headers=secretref:otel-headers"
```

## 4. Available Dashboards
Import these Grafana dashboards for .NET:
- **ASP.NET Core** (ID: 19924) — request rate, latency, errors
- **OpenTelemetry Collector** (ID: 15983) — pipeline health

## 5. Custom Metrics Available
The API emits these custom metrics:
- `onboarding.sessions_created` — counter of new onboarding sessions
- `onboarding.state_transitions` — counter with `from_state` and `to_state` tags
- `cma.jobs_submitted` — counter of CMA pipeline runs
- `cma.pipeline_duration` — histogram of pipeline execution time
```

- [ ] **Step 4: Commit**

```bash
git add apps/api/RealEstateStar.Api/appsettings.Production.json
git add infra/grafana/README.md
git commit -m "infra: configure OTLP export to Grafana Cloud for monitoring"
```

---

## Chunk 5: DNS + Custom Domains

### Task 8: Document DNS Configuration

**Files:**
- Create: `infra/cloudflare/README.md`

**Context:** The domain `realestatestar.com` is already owned. Cloudflare manages DNS. We need records for: the platform, agent-site wildcard, and API.

- [ ] **Step 1: Create DNS setup documentation**

```markdown
# Cloudflare DNS Configuration

## Required DNS Records

| Type | Name | Target | Proxy |
|------|------|--------|-------|
| CNAME | `@` | `real-estate-star-platform.pages.dev` | Yes |
| CNAME | `www` | `real-estate-star-platform.pages.dev` | Yes |
| CNAME | `*` | `real-estate-star-agents.pages.dev` | Yes |
| CNAME | `api` | `real-estate-star-api.<region>.azurecontainerapps.io` | Yes (DNS only if SSL issues) |

## Cloudflare Pages Custom Domains

### Platform (`realestatestar.com`)
1. Go to Workers & Pages → `real-estate-star-platform` → Custom domains
2. Add `realestatestar.com` and `www.realestatestar.com`
3. Cloudflare auto-provisions SSL

### Agent Sites (`*.realestatestar.com`)
1. Go to Workers & Pages → `real-estate-star-agents` → Custom domains
2. Add `*.realestatestar.com` (wildcard)
3. The middleware routes by subdomain → agent slug

### API (`api.realestatestar.com`)
1. Get the Azure Container App FQDN:
   ```bash
   az containerapp show --name real-estate-star-api --resource-group real-estate-star-rg \
     --query "properties.configuration.ingress.fqdn" -o tsv
   ```
2. Add CNAME record in Cloudflare: `api` → that FQDN
3. Configure custom domain on Azure:
   ```bash
   az containerapp hostname add \
     --name real-estate-star-api \
     --resource-group real-estate-star-rg \
     --hostname api.realestatestar.com
   ```

## SSL/TLS Settings
- SSL mode: **Full (strict)** — both Cloudflare and origin have valid certs
- Always Use HTTPS: **On**
- Minimum TLS Version: **1.2**
- Automatic HTTPS Rewrites: **On**

## Security Headers (Cloudflare Rules)
Already handled by the API middleware (X-Content-Type-Options, X-Frame-Options, HSTS, Referrer-Policy).

## Page Rules
- `api.realestatestar.com/*` → Cache Level: Bypass (API responses should never be cached)
```

- [ ] **Step 2: Commit**

```bash
git add infra/cloudflare/README.md
git commit -m "docs: add Cloudflare DNS and custom domain setup guide"
```

---

## Chunk 6: Missing GitHub Secrets + Production Readiness

### Task 9: Identify and Set Missing GitHub Secrets

**Files:** None (GitHub secrets only)

**Context:** Current secrets: `ANTHROPIC_API_KEY`, `CLOUDFLARE_ACCOUNT_ID`, `CLOUDFLARE_API_TOKEN`, `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET`, `SCRAPER_API_KEY`, `STRIPE_SECRET_KEY`. Missing secrets needed by workflows.

- [ ] **Step 1: Set missing secrets via CLI**

```bash
# API URL (set after Azure deploy — the Container App FQDN)
gh secret set API_URL --body "https://api.realestatestar.com"

# Stripe publishable key (needed by platform frontend build)
gh secret set STRIPE_PUBLISHABLE_KEY --body "pk_live_XXXXX"

# Stripe webhook secret (needed by API)
gh secret set STRIPE_WEBHOOK_SECRET --body "whsec_XXXXX"

# Attom API key (needed by CMA pipeline)
gh secret set ATTOM_API_KEY --body "XXXXX"

# Azure credentials (JSON from create-gh-credentials.sh)
gh secret set AZURE_CREDENTIALS --body '{"clientId":"...","clientSecret":"...","subscriptionId":"...","tenantId":"..."}'
```

- [ ] **Step 2: Verify all secrets exist**

```bash
gh secret list
```

Expected: `ANTHROPIC_API_KEY`, `API_URL`, `ATTOM_API_KEY`, `AZURE_CREDENTIALS`, `CLOUDFLARE_ACCOUNT_ID`, `CLOUDFLARE_API_TOKEN`, `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET`, `SCRAPER_API_KEY`, `STRIPE_PUBLISHABLE_KEY`, `STRIPE_SECRET_KEY`, `STRIPE_WEBHOOK_SECRET`

### Task 10: Create Production Readiness Checklist

**Files:**
- Create: `docs/production-checklist.md`

- [ ] **Step 1: Create checklist**

```markdown
# Production Readiness Checklist

## Infrastructure
- [ ] Azure Container Apps resource created (`infra/azure/setup.sh`)
- [ ] Azure secrets configured (`infra/azure/set-secrets.sh`)
- [ ] GitHub service principal created (`infra/azure/create-gh-credentials.sh`)
- [ ] All GitHub secrets set (12 total)
- [ ] Docker image builds and runs locally

## DNS & Domains
- [ ] `realestatestar.com` → Cloudflare Pages (platform)
- [ ] `www.realestatestar.com` → Cloudflare Pages (platform)
- [ ] `*.realestatestar.com` → Cloudflare Pages (agent-site)
- [ ] `api.realestatestar.com` → Azure Container Apps
- [ ] SSL/TLS set to Full (strict)
- [ ] HTTPS enforced

## Monitoring
- [ ] Grafana Cloud account created
- [ ] OTLP endpoint configured on Azure
- [ ] Custom dashboards imported
- [ ] Alert on: error rate > 1%, p99 latency > 5s, pod restarts

## Stripe
- [ ] Live mode keys set (not test keys)
- [ ] Webhook endpoint registered: `https://api.realestatestar.com/webhooks/stripe`
- [ ] Webhook events: `checkout.session.completed`, `customer.subscription.deleted`
- [ ] Test webhook with Stripe CLI: `stripe trigger checkout.session.completed`

## Google OAuth
- [ ] Production redirect URI: `https://api.realestatestar.com/oauth/google/callback`
- [ ] OAuth consent screen approved (or in test mode with allowed test users)
- [ ] Scopes: `email`, `profile`, `https://www.googleapis.com/auth/gmail.readonly`

## Security
- [ ] Branch protection re-enabled on `main`
- [ ] All API keys rotated from dev values
- [ ] No secrets in git history (filter-repo already run)
- [ ] Rate limiting verified in production
- [ ] CORS only allows production origins

## Smoke Tests
- [ ] `GET https://api.realestatestar.com/health/live` → 200
- [ ] `GET https://api.realestatestar.com/health/ready` → 200
- [ ] `POST https://api.realestatestar.com/onboard` → creates session
- [ ] Platform loads at `https://realestatestar.com`
- [ ] Agent site loads at `https://jenise-buckalew.realestatestar.com`
- [ ] Onboarding chat flow works end-to-end
- [ ] CMA form submits and generates report
```

- [ ] **Step 2: Commit**

```bash
git add docs/production-checklist.md
git commit -m "docs: add production readiness checklist"
```

---

## Execution Order

```
Task 1 (appsettings.Production) ──┐
Task 2 (Dockerfile)               ├─→ Task 5 (GH Actions) ─→ Task 9 (secrets)
Task 3 (Azure setup.sh)           │
Task 4 (Azure secrets.sh) ────────┘
Task 6 (GH credentials) ──────────────→ Task 9 (secrets)
Task 7 (Grafana OTLP) ────────────────→ standalone
Task 8 (DNS docs) ────────────────────→ standalone
Task 10 (checklist) ──────────────────→ standalone
```

Tasks 1-4 and 6-8 are all independent and can run in parallel.
Tasks 5 and 9 depend on earlier tasks.
Task 10 is the final verification checklist.

## Manual Steps (cannot be automated — require Eddie)

1. **Run `infra/azure/setup.sh`** — requires `az login` with your Azure account
2. **Run `infra/azure/set-secrets.sh`** — requires actual API key values
3. **Run `infra/azure/create-gh-credentials.sh`** — requires Azure admin access
4. **Set GitHub secrets** — `AZURE_CREDENTIALS`, `API_URL`, `STRIPE_PUBLISHABLE_KEY`, `STRIPE_WEBHOOK_SECRET`, `ATTOM_API_KEY`
5. **Configure Cloudflare DNS records** — via Cloudflare dashboard
6. **Create Grafana Cloud account** — sign up at grafana.com
7. **Register Stripe webhook** — via Stripe dashboard
8. **Update Google OAuth redirect URI** — via Google Cloud Console
