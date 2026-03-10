# Deployment Guide

## Architecture

```
Internet → Cloudflare Pages (platform) → Railway (API) → External Services
                                                          ├── Claude API
                                                          ├── Google OAuth + APIs
                                                          ├── Stripe Checkout
                                                          └── Cloudflare Pages (agent sites)
```

## Prerequisites

You need accounts on these services (all have free tiers):

| Service | URL | Purpose |
|---------|-----|---------|
| Railway | https://railway.app | API hosting ($5/mo free credit) |
| Cloudflare | https://dash.cloudflare.com | Frontend hosting (free) |
| Anthropic | https://console.anthropic.com | Claude API |
| Stripe | https://dashboard.stripe.com | Payment processing |
| Google Cloud | https://console.cloud.google.com | OAuth + Gmail/Drive/Sheets |

---

## Step 1: Anthropic API Key (~2 min)

1. Go to https://console.anthropic.com
2. Sign in or create account
3. Navigate to **API Keys** in the left sidebar
4. Click **Create Key**
5. Name it `real-estate-star`
6. Copy the key (starts with `sk-ant-`)

**Save:** `ANTHROPIC_API_KEY`

---

## Step 2: Stripe Account (~5 min)

1. Go to https://dashboard.stripe.com/register
2. Create account (no business verification needed for test mode)
3. Toggle **Test mode** (top right)

### Get API Keys
4. Go to **Developers → API Keys**
5. Copy **Publishable key** (starts with `pk_test_`)
6. Copy **Secret key** (starts with `sk_test_`)

### Create Product
7. Go to **Products → Add Product**
8. Name: `Real Estate Star - One-Time Setup`
9. Price: `$900.00` one-time
10. Click **Save**
11. Copy the **Price ID** from the product page (starts with `price_`)

### Set Up Webhook
12. Go to **Developers → Webhooks → Add endpoint**
13. URL: `https://<your-api-url>/webhooks/stripe` (set after Railway deploy)
14. Events: Select `checkout.session.completed`
15. Click **Add endpoint**
16. Copy the **Signing secret** (starts with `whsec_`)

**Save:**
- `STRIPE_SECRET_KEY` (sk_test_...)
- `STRIPE_PUBLISHABLE_KEY` (pk_test_...)
- `STRIPE_PRICE_ID` (price_...)
- `STRIPE_WEBHOOK_SECRET` (whsec_...)

---

## Step 3: Google Cloud OAuth (~5 min)

1. Go to https://console.cloud.google.com
2. Create a new project: `Real Estate Star`

### Configure OAuth Consent Screen
3. Navigate to **APIs & Services → OAuth consent screen**
4. User type: **External**
5. App name: `Real Estate Star`
6. Support email: your email
7. Scopes: Add these 7 scopes:
   - `userinfo.profile`
   - `userinfo.email`
   - `gmail.send`
   - `drive.file`
   - `documents`
   - `spreadsheets`
   - `calendar.events`
8. Test users: Add your email
9. Save

### Create OAuth Credentials
10. Navigate to **APIs & Services → Credentials**
11. Click **Create Credentials → OAuth client ID**
12. Type: **Web application**
13. Name: `Real Estate Star`
14. Authorized redirect URIs:
    - `http://localhost:5000/oauth/google/callback` (development)
    - `https://<your-api-url>/oauth/google/callback` (production — add after Railway deploy)
15. Copy **Client ID** and **Client Secret**

### Enable APIs
16. Navigate to **APIs & Services → Library**
17. Enable: Gmail API, Google Drive API, Google Docs API, Google Sheets API, Google Calendar API

**Save:**
- `GOOGLE_CLIENT_ID`
- `GOOGLE_CLIENT_SECRET`

---

## Step 4: Cloudflare Account (~3 min)

1. Go to https://dash.cloudflare.com
2. Sign up or sign in
3. Copy your **Account ID** (right sidebar on main dashboard)

### Create API Token
4. Go to **My Profile → API Tokens → Create Token**
5. Use template: **Edit Cloudflare Workers**
6. Permissions: `Cloudflare Pages:Edit`
7. Click **Create Token**
8. Copy the token

**Save:**
- `CLOUDFLARE_ACCOUNT_ID`
- `CLOUDFLARE_API_TOKEN`

---

## Step 5: Deploy API to Railway (~3 min)

1. Go to https://railway.app and sign in with GitHub
2. Click **New Project → Deploy from GitHub Repo**
3. Select `edward-rosado/jenisesells-website`
4. Railway will auto-detect the Dockerfile at `apps/api/Dockerfile`
5. If not, set:
   - **Root Directory:** `apps/api`
   - **Builder:** Dockerfile

### Set Environment Variables
6. In Railway project settings → **Variables**, add:

```
ASPNETCORE_ENVIRONMENT=Production
Anthropic__ApiKey=<your-anthropic-key>
Attom__ApiKey=<your-attom-key>
Google__ClientId=<your-google-client-id>
Google__ClientSecret=<your-google-client-secret>
Google__RedirectUri=https://<railway-url>/oauth/google/callback
Stripe__SecretKey=<your-stripe-secret-key>
Stripe__WebhookSecret=<your-stripe-webhook-secret>
Stripe__PriceId=<your-stripe-price-id>
Platform__BaseUrl=https://real-estate-star-platform.pages.dev
Cors__AllowedOrigins__0=https://real-estate-star-platform.pages.dev
```

> **Note:** Railway uses `__` (double underscore) for nested config keys, mapping to `:` in .NET configuration.

### Get Deploy Token
7. Go to **Account Settings → Tokens → Create Token**
8. Copy the token

### Get Service ID
9. In your project, click the service → **Settings**
10. Copy the **Service ID**

**Save:**
- `RAILWAY_TOKEN`
- `RAILWAY_SERVICE_ID`
- Railway public URL (for Stripe webhook + Google redirect URI)

---

## Step 6: Deploy Platform to Cloudflare Pages (~2 min)

The deploy workflow handles this automatically. You just need GitHub Secrets configured.

Alternatively, for initial setup:
1. Go to Cloudflare Dashboard → **Workers & Pages → Create**
2. Connect to GitHub → Select repo
3. Build settings:
   - **Framework:** Next.js
   - **Root directory:** `apps/platform`
   - **Build command:** `npm run build`
   - **Build output directory:** `.next`
4. Environment variables:
   - `NEXT_PUBLIC_API_URL`: Your Railway API URL
   - `NEXT_PUBLIC_STRIPE_KEY`: Your Stripe publishable key

---

## Step 7: Configure GitHub Secrets

Go to your repo → **Settings → Secrets and variables → Actions** and add:

| Secret Name | Value |
|-------------|-------|
| `RAILWAY_TOKEN` | Railway deploy token |
| `RAILWAY_SERVICE_ID` | Railway service ID |
| `CLOUDFLARE_API_TOKEN` | Cloudflare API token |
| `CLOUDFLARE_ACCOUNT_ID` | Cloudflare account ID |
| `API_URL` | Railway public URL (e.g., `https://real-estate-star-api.up.railway.app`) |
| `STRIPE_PUBLISHABLE_KEY` | Stripe publishable key (pk_test_...) |

---

## Step 8: Update Production URLs

After deploying, update these with your actual URLs:

1. **Stripe Webhook URL:** Dashboard → Developers → Webhooks → Update endpoint URL
2. **Google OAuth Redirect URI:** Cloud Console → Credentials → Edit OAuth client → Add production redirect URI
3. **Railway `Google__RedirectUri`:** Update to production callback URL
4. **Railway `Platform__BaseUrl`:** Update to Cloudflare Pages URL
5. **Railway `Cors__AllowedOrigins__0`:** Update to Cloudflare Pages URL

---

## Local Development

```bash
# API (from apps/api/)
dotnet run --project RealEstateStar.Api

# Platform (from apps/platform/)
npm run dev

# With observability stack
docker compose -f apps/api/docker-compose.observability.yml up
```

Required local config in `apps/api/RealEstateStar.Api/appsettings.Development.json` or environment variables.
