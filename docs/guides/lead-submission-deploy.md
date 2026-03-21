# Lead Submission API — Deployment & Testing Guide

**Last Updated:** 2026-03-20
**Scope:** Lead submission endpoints, notifications, opt-out/deletion flows

## Prerequisites

### Azure Resources
- **Resource Group:** `real-estate-star-rg`
- **Container App:** `real-estate-star-api` (with system-assigned managed identity)
- **Container Registry:** `realestatestar.azurecr.io`
- **Storage Account:** `realestatestarsa` (for DataProtection key ring)
- **Key Vault:** `real-estate-star-kv` (for secrets management)

### Cloudflare Resources
- **Domain:** `real-estate-star.com` (DNS configured)
- **Worker Routes:**
  - `api.real-estate-star.com/*` → Azure Container Apps
  - `{handle}.real-estate-star.com/*` → Agent site (Cloudflare Workers)

### Required Configuration Files
- Agent config files at `config/agents/{agent-id}.json`
- `.env` file at repository root (for local development only)

---

## 1. Azure Infrastructure Setup (One-Time)

### Step 1: Create Resource Group

```bash
az group create \
  --name real-estate-star-rg \
  --location eastus
```

### Step 2: Create Container App (minimal config)

```bash
az containerapp create \
  --name real-estate-star-api \
  --resource-group real-estate-star-rg \
  --image mcr.microsoft.com/dotnet/aspnet:10.0 \
  --cpu 0.5 \
  --memory 1.0Gi \
  --ingress external \
  --target-port 8080 \
  --allow-insecure
```

### Step 3: Create Storage and Key Vault

Run the provisioning script:

```bash
pwsh infra/azure/provision-keyvault.ps1
```

This script:
- Creates storage account `realestatestarsa`
- Creates blob container `dataprotection`
- Creates Key Vault `real-estate-star-kv`
- Assigns managed identity permissions to the Container App
- Outputs env vars to set on the Container App

### Step 4: Set Environment Variables on Container App

After running the provision script, set these env vars (replace with actual values):

```bash
az containerapp update \
  --name real-estate-star-api \
  --resource-group real-estate-star-rg \
  --set-env-vars \
    "ASPNETCORE_ENVIRONMENT=Production" \
    "Anthropic__ApiKey=sk-ant-..." \
    "Attom__ApiKey=your_attom_key" \
    "Google__ClientId=your_google_client_id" \
    "Google__ClientSecret=your_google_client_secret" \
    "Google__RedirectUri=https://api.real-estate-star.com/auth/google/callback" \
    "Stripe__SecretKey=sk_live_..." \
    "Stripe__WebhookSecret=whsec_..." \
    "Stripe__PriceId=price_..." \
    "Platform__BaseUrl=https://platform.real-estate-star.com" \
    "ScraperApi__ApiKey=your_scraper_api_key" \
    "Cloudflare__ApiToken=your_cloudflare_api_token" \
    "Cloudflare__AccountId=your_cloudflare_account_id" \
    "Turnstile__SiteKey=your_turnstile_site_key" \
    "Turnstile__SecretKey=your_turnstile_secret_key" \
    "AzureKeyVault__VaultUri=https://real-estate-star-kv.vault.azure.net" \
    "DataProtection__BlobUri=https://realestatestarsa.blob.core.windows.net/dataprotection/keys.xml" \
    "AzureStorage__ConnectionString=DefaultEndpointsProtocol=https;AccountName=realestatestarsa;..." \
    "WhatsApp__PhoneNumberId=your_phone_number_id" \
    "WhatsApp__AccessToken=your_whatsapp_access_token" \
    "WhatsApp__AppSecret=your_whatsapp_app_secret" \
    "WhatsApp__VerifyToken=your_whatsapp_verify_token" \
    "WhatsApp__WabaId=your_waba_id"
```

---

## 2. Cloudflare Setup (One-Time)

### Step 1: Create Turnstile Widget

1. Go to Cloudflare dashboard → Turnstile
2. Click "Create Site"
3. Set **Domain:** `real-estate-star.com`
4. Choose **Mode:** Managed (recommended) or Challenge
5. Copy **Site Key** and **Secret Key**
6. Store in Azure Container App env vars (see above)

### Step 2: Store Frontend Environment Variables

Agent site needs `NEXT_PUBLIC_TURNSTILE_SITE_KEY` at build time.

In `.github/workflows/deploy-agent-site.yml`:
```yaml
env:
  NEXT_PUBLIC_API_URL: ${{ secrets.API_URL }}
  NEXT_PUBLIC_GOOGLE_MAPS_API_KEY: ${{ secrets.GOOGLE_MAPS_API_KEY }}
  NEXT_PUBLIC_TURNSTILE_SITE_KEY: ${{ secrets.TURNSTILE_SITE_KEY }}
```

### Step 3: Verify Custom Domain Routes

In Cloudflare dashboard:
- Confirm `api.real-estate-star.com` routes to Azure Container Apps FQDN
- Confirm `{handle}.real-estate-star.com` routes to Cloudflare Workers

---

## 3. Per-Agent Configuration

### Step 1: Create Agent Config File

Copy template:
```bash
cp config/agents/jenise-buckalew.json config/agents/your-agent-id.json
```

Update all fields:
```json
{
  "id": "your-agent-id",
  "identity": {
    "name": "Agent Full Name",
    "email": "agent@example.com",
    "phone": "+1-555-1234",
    "licenseNumber": "NJ-123456",
    "brokerage": "Broker Name, Inc.",
    "brokeragePhone": "+1-555-5678",
    "brokerageAddress": "123 Main St, New Jersey, NJ 08001"
  },
  "location": {
    "state": "NJ",
    "serviceAreas": ["Hudson County", "Bergen County"],
    "officeAddress": "123 Main St, Hoboken, NJ 07030"
  },
  "branding": {
    "siteName": "Your Agent Site",
    "primaryColor": "#0066cc",
    "accentColor": "#ff6600",
    "logoUrl": "https://your-domain.com/logo.png",
    "headerPhone": "+1-555-1234",
    "headerEmail": "contact@your-domain.com"
  },
  "integrations": {
    "googleDriveRootFolderId": "your_folder_id",
    "googleChatWebhookUrl": "https://chat.googleapis.com/v1/spaces/...",
    "gmailFromAddress": "agent@example.com"
  },
  "compliance": {
    "copyrightYear": 2026,
    "copyrightName": "Your Name"
  }
}
```

Validate against schema:
```bash
npx ajv validate -s config/agent.schema.json -d config/agents/your-agent-id.json
```

### Step 2: Generate API Key for Agent

Generate a cryptographically secure API key:
```bash
# macOS/Linux
openssl rand -hex 32

# Windows PowerShell
[System.BitConverter]::ToString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32)) -replace '-',''
```

Example: `a1b2c3d4e5f6...` (64 hex characters)

### Step 3: Set HMAC Configuration on API

Configure the API key mapping in the Container App. The middleware validates that:
1. The `X-API-Key` header matches a registered key
2. The key maps to the agent ID in the URL

Store as secrets in Azure:
```bash
# Format: "key-name=agent-id"
az containerapp secret set \
  --name real-estate-star-api \
  --resource-group real-estate-star-rg \
  --secrets \
    "hmac-secret-base=your-64-char-secret" \
    "api-key-your-agent-id=your_api_key"
```

Then set env vars:
```bash
az containerapp update \
  --name real-estate-star-api \
  --resource-group real-estate-star-rg \
  --set-env-vars \
    "HmacAuth__HmacSecret=secretref:hmac-secret-base" \
    "HmacAuth__ApiKeys__your_api_key=your-agent-id"
```

### Step 4: Set Up Google Drive Folder Structure

The agent site will auto-create this structure on first submission:
```
Google Drive Root (from config)
├── 📁 leads/
│   └── 📄 {firstName}-{lastName}-{leadId}.md
├── 📁 consent-logs/
│   └── 📄 {agentId}-marketing-consent.jsonl
└── 📁 deletions/
    └── 📄 {agentId}-deletion-audit.jsonl
```

Verify the root folder ID is set in `config/agents/{agent-id}.json`:
```json
"integrations": {
  "googleDriveRootFolderId": "1ABC...xyz123..."
}
```

### Step 5: Set Up Google Chat Webhook (Optional)

For lead notifications in Google Chat:

1. Go to Google Chat → your space
2. **Apps & integrations** → **Webhooks** → **Create new webhook**
3. Name: "Real Estate Star Lead Alerts"
4. Copy the webhook URL
5. Update agent config:
```json
"integrations": {
  "googleChatWebhookUrl": "https://chat.googleapis.com/v1/spaces/ABC.../messages?key=..."
}
```

Agent will now receive Google Chat cards for each new lead with:
- Lead score and motivation
- Contact info and timeline
- Selling/buying details
- Enrichment summary

---

## 4. Deployment Steps

### API Deployment

All pushes to `main` branch that modify `apps/api/` trigger the deploy pipeline:

```bash
# Merge PR to main
git checkout main
git pull origin main

# Pipeline runs automatically:
# 1. Build & test (.NET 10)
# 2. Build Docker image
# 3. Push to ACR (Azure Container Registry)
# 4. Update Container App
# 5. Health check: /health/live and /health/ready
# 6. Auto-rollback on failure
```

View logs:
```bash
# GitHub Actions
gh run list --workflow deploy-api.yml

# Container App logs
az containerapp logs show \
  --name real-estate-star-api \
  --resource-group real-estate-star-rg \
  --tail 100
```

### Agent Site Deployment

All pushes to `main` that modify `apps/agent-site/`, `packages/`, or `config/` trigger the deploy pipeline:

```bash
# Pipeline runs automatically:
# 1. Lint & test (Vitest, 100% coverage enforced)
# 2. Generate config registry (prebuild step)
# 3. Build Next.js
# 4. OpenNext transform to Workers format
# 5. Deploy to Cloudflare Workers via wrangler
```

View logs:
```bash
gh run list --workflow deploy-agent-site.yml
```

---

## 5. Manual Testing Checklist

### Local Development Setup

#### Start the API

```bash
cd apps/api
dotnet run --project RealEstateStar.Api/RealEstateStar.Api.csproj

# API runs on http://localhost:5135
```

Create `.env` at repository root (DO NOT commit):
```bash
ASPNETCORE_ENVIRONMENT=Development
ANTHROPIC_API_KEY=your_key
GOOGLE_CLIENT_ID=your_id
GOOGLE_CLIENT_SECRET=your_secret
GOOGLE_REDIRECT_URI=http://localhost:5135/auth/google/callback
STRIPE_SECRET_KEY=your_key
STRIPE_WEBHOOK_SECRET=your_secret
STRIPE_PRICE_ID=your_id
PLATFORM_BASE_URL=http://localhost:3000
TURNSTILE_SECRET_KEY=your_secret
FILE_STORAGE__BASE_PATH=~/real-estate-star/storage
```

#### Start the Agent Site

```bash
cd apps/agent-site
npm run dev

# Agent site runs on http://localhost:3000
# Access test agent at: http://jenise-buckalew.localhost:3000
```

#### Required .env.local for Agent Site

```bash
NEXT_PUBLIC_API_URL=http://localhost:5135
NEXT_PUBLIC_GOOGLE_MAPS_API_KEY=AIzaSyCD8Lw5xaMCxoWEzkTRCT284Jv37AMfd2Y
NEXT_PUBLIC_TURNSTILE_SITE_KEY=1x00000000000000000000AA
```

### Test 1: Submit a Lead

1. Open http://jenise-buckalew.localhost:3000 in browser
2. Fill lead form:
   - Name: "Test Buyer"
   - Email: "test@example.com"
   - Phone: "+1-555-1234"
   - Lead Type: "Buying"
   - City: "Hoboken"
   - State: "NJ"
   - Max Budget: "$500,000"
   - Bedrooms: 2
   - Bathrooms: 1.5
   - Marketing Consent: Check
3. Click "Submit Lead"

**Expected Results:**
- Status: "Lead received"
- Lead ID displayed on success page (e.g., `550e8400-e29b-...`)
- In API logs: `[LEAD-001]` messages for save, enrichment, notification
- Google Drive: New markdown file created at `leads/{FirstName}-{LastName}-{leadId}.md`
- Google Chat (if configured): Notification card appears in agent's space

**If Lead Storage is Local (Development):**
```bash
# Check created file
ls ~/real-estate-star/storage/leads/

# Example: jenise-buckalew/550e8400-e29b-41d8-a925-275a2ce73c0e.md
```

### Test 2: Verify Marketing Consent Logging

1. Submit another lead with different email
2. Check Google Drive for consent log:
   ```
   /consent-logs/jenise-buckalew-marketing-consent.jsonl
   ```

Expected format (JSONL — one JSON object per line):
```json
{"LeadId":"550e8400-e29b-...", "Email":"test@example.com", "FirstName":"Test", "OptedIn":true, "Channels":["email","sms"], "IpAddress":"127.0.0.1", "Timestamp":"2026-03-20T15:30:45Z"}
```

### Test 3: Test Opt-Out Flow

1. From submitted lead, navigate to opt-out page
2. Example URL: `{agent-site}/leads/opt-out?email=test@example.com`
3. Click "Unsubscribe"

**Expected Results:**
- OptOut endpoint called: `POST /agents/{agentId}/leads/opt-out`
- Status: "You have been unsubscribed"
- Lead status updated to "opted_out" in storage
- Marketing consent redacted from log

### Test 4: Test Deletion Request Flow

1. From submitted lead, navigate to deletion page
2. Example URL: `{agent-site}/leads/delete?email=test@example.com`
3. Click "Delete My Data"

**Expected Results:**
- RequestDeletion endpoint called: `POST /agents/{agentId}/leads/request-deletion`
- Returns deletion token (e.g., `del_abc123...`)
- Token shown on page or emailed to user
4. User submits token on verification page
5. DeleteData endpoint called: `POST /agents/{agentId}/leads/delete`

**Final Results:**
- Lead document deleted from Google Drive
- Marketing consent records redacted
- Deletion audit log created: `/deletions/{agentId}-deletion-audit.jsonl`

### Test 5: Verify Seller CMA Trigger

1. Submit lead with "Selling" type:
   - Property Address: "123 Main St, Hoboken, NJ 07030"
   - Beds: 3
   - Baths: 2
   - Property Condition: "Good"
2. Check `/health/ready` in API logs

**Expected Results:**
- CMA job created in memory store
- CMA pipeline executes asynchronously
- SignalR hub sends progress updates to client
- PDF generated and stored in Google Drive (if pipeline enabled)

### Test 6: Verify Buyer Home Search

1. Submit lead with "Buying" type:
   - Desired Area: Hoboken, NJ
   - Max Budget: $750,000
   - Bedrooms: 2+

**Expected Results:**
- HomeSearchProvider queries Zillow/Redfin/Realtor
- Listings results returned (if search hits)
- Lead updated with search ID

### Test 7: API Health Checks

Test both health endpoints:

```bash
# Liveness — no dependencies
curl http://localhost:5135/health/live
# Expected: 200 OK, empty body

# Readiness — checks Claude API, Turnstile config
curl http://localhost:5135/health/ready
# Expected: 200 OK with JSON
# {
#   "status": "Healthy",
#   "checks": [
#     { "name": "claude_api", "status": "Healthy", "duration": 45 }
#   ]
# }
```

### Test 8: Rate Limiting

Verify rate limiting is enforced:

```bash
# Global: 100 requests/min per IP
for i in {1..101}; do
  curl -s http://localhost:5135/health/live -o /dev/null -w "%{http_code}\n"
done
# 101st request should return 429 Too Many Requests

# Lead creation: 10 per hour per agent
for i in {1..11}; do
  curl -X POST http://localhost:5135/agents/jenise-buckalew/leads \
    -H "Content-Type: application/json" \
    -d '{...lead data...}'
done
# 11th request should return 429
```

---

## 6. Production Smoke Tests

After deploying to production, run these checks:

### Test 1: Live API Endpoints

```bash
# Health checks
curl https://api.real-estate-star.com/health/live
curl https://api.real-estate-star.com/health/ready

# Verify Turnstile is configured
# If degraded, check Azure Container App env vars
```

### Test 2: Submit Test Lead via Live Agent Site

1. Open https://jenise-buckalew.real-estate-star.com
2. Submit a test lead (use clear test data)
3. Verify success response

### Test 3: Check Google Drive for Lead Document

1. Open Google Drive
2. Navigate to root folder from agent config
3. Verify new file in `/leads` folder with markdown content

Example file structure:
```markdown
---
leadId: 550e8400-e29b-...
email: test@example.com
phone: +1-555-1234
receivedAt: 2026-03-20T15:30:45Z
---

# Test Buyer

...lead details in markdown...
```

### Test 4: Verify Google Chat Notification (if configured)

1. Check agent's Google Chat space
2. Verify lead card appears with:
   - Lead score
   - Contact info
   - Lead type (Buying/Selling)
   - Enrichment summary

### Test 5: Verify Email Notification

1. Check agent's email inbox for:
   - Subject: "New Lead: {FullName} — {Category} (Score: XX)"
   - Body: Markdown formatted email with enrichment details

### Test 6: Test Opt-Out on Production

1. Visit opt-out page for test lead
2. Unsubscribe and verify status page
3. Check Google Drive consent log for redaction

### Test 7: Test Deletion on Production

1. Visit deletion request page for test lead
2. Request deletion and capture token
3. Submit token on verification page
4. Verify:
   - Lead document deleted from Google Drive
   - Deletion audit log created

---

## 7. Troubleshooting

### Common Issues & Fixes

#### API Returns 401 Unauthorized

**Cause:** Missing or invalid HMAC signature

**Check:**
```bash
# Verify X-API-Key header is set
# Verify X-Signature header matches: sha256={hex}
# Verify X-Timestamp header is recent (drift < 300s)

# Logs show: [LEAD-019], [LEAD-020], [LEAD-021], [LEAD-022]
```

**Fix:**
```bash
# Regenerate API key and update client
# Ensure client clock is synchronized (NTP)
# Verify HmacAuth__ApiKeys config on Container App
```

#### Health Check Returns Degraded

**Cause:** Turnstile secret key not configured

**Check:**
```bash
curl https://api.real-estate-star.com/health/ready | jq
```

**Fix:**
```bash
az containerapp update \
  --name real-estate-star-api \
  --resource-group real-estate-star-rg \
  --set-env-vars "Turnstile__SecretKey=..."
```

#### Lead Not Appearing in Google Drive

**Cause:** Missing Google auth or folder ID

**Check logs:**
```bash
az containerapp logs show \
  --name real-estate-star-api \
  --resource-group real-estate-star-rg \
  --tail 100 | grep -i "LEAD-005\|LEAD-034"
```

**Fix:**
1. Verify agent config has `googleDriveRootFolderId`
2. Check Google API credentials in Key Vault
3. Verify Container App managed identity can access Google Drive

#### Agent Site Shows "API Unavailable"

**Cause:** CORS rejection or API unreachable

**Check:**
```bash
# Verify NEXT_PUBLIC_API_URL in build
# Check browser console for CORS errors
# Test: curl https://api.real-estate-star.com/health/live
```

**Fix:**
1. Verify `Cors:AllowedOrigins` on API includes agent site domain
2. Verify Cloudflare routing is correct
3. Re-deploy agent site with correct `NEXT_PUBLIC_API_URL`

#### Stripe Webhook Not Triggered

**Cause:** Webhook secret mismatch or endpoint unreachable

**Check:**
```bash
az containerapp update \
  --name real-estate-star-api \
  --resource-group real-estate-star-rg \
  --query "properties.template.containers[].env" | jq '.[] | select(.name == "Stripe__WebhookSecret")'
```

**Fix:**
1. Copy webhook signing secret from Stripe dashboard (not API key)
2. Update `Stripe__WebhookSecret` env var
3. Re-run CI/CD to deploy

#### Rate Limit Blocking Legitimate Traffic

**Cause:** IP not properly forwarded through proxy

**Check logs:**
```bash
# Logs show RemoteIpAddress as proxy IP (e.g., Cloudflare IP)
# Not the user's real IP
```

**Fix:**
- ForwardedHeaders middleware is enabled in Program.cs (line 386)
- Verify Cloudflare is trusted proxy (KnownProxies)

---

## 8. Monitoring & Observability

### View API Logs

```bash
# Last 100 lines
az containerapp logs show \
  --name real-estate-star-api \
  --resource-group real-estate-star-rg \
  --tail 100 \
  --follow

# Search for lead-related errors
az containerapp logs show \
  --name real-estate-star-api \
  --resource-group real-estate-star-rg | grep "LEAD-"
```

### Error Code Reference

| Code | Message | Action |
|------|---------|--------|
| LEAD-001 | Lead saved | Normal flow |
| LEAD-004 | CMA trigger failed | Check CMA pipeline logs |
| LEAD-005 | Notification failed | Check Google Chat/Gmail config |
| LEAD-006 | Home search failed | Check home search provider |
| LEAD-007 | Enrichment failed | Check Claude API, check logs |
| LEAD-019 | Invalid API key | Verify X-API-Key header |
| LEAD-020 | API key agent mismatch | Verify key maps to correct agent |
| LEAD-021 | HMAC signature invalid | Verify signing secret |
| LEAD-022 | Timestamp drift | Sync system clock |
| LEAD-033 | Google Chat webhook failed | Check webhook URL, continue to email |
| LEAD-034 | Gmail notification failed | Check Gmail config, check gws CLI |

### OpenTelemetry Metrics

If Grafana Cloud is configured:
```bash
# View metrics dashboard
# - lead_submissions_total (counter)
# - lead_enrichment_duration_ms (histogram)
# - notification_delivery_time_ms (histogram)
```

---

## 9. Rollback Procedure

If a deploy fails or causes issues:

```bash
# Auto-rollback (built into deploy-api.yml)
# If /health/ready fails, previous revision is activated

# Manual rollback to previous revision
PREV_REVISION=$(az containerapp revision list \
  --name real-estate-star-api \
  --resource-group real-estate-star-rg \
  --query "[1].name" -o tsv)

az containerapp ingress traffic set \
  --name real-estate-star-api \
  --resource-group real-estate-star-rg \
  --revision-weight "$PREV_REVISION=100"
```

---

## References

- **Lead Submission API:** `/agents/{agentId}/leads` (POST)
- **Opt-Out:** `/agents/{agentId}/leads/opt-out` (POST)
- **Request Deletion:** `/agents/{agentId}/leads/request-deletion` (POST)
- **Execute Deletion:** `/agents/{agentId}/leads/delete` (POST)
- **Agent Config:** `config/agent.schema.json`
- **Deployment Workflow:** `.github/workflows/deploy-api.yml`
