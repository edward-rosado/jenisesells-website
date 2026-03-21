# Cloudflare Worker Secrets Setup

The agent site Cloudflare Worker needs 4 runtime secrets that are **not** in `wrangler.jsonc` (they'd be plaintext). These must be set via `wrangler secret bulk` or the Cloudflare dashboard.

## Secrets Required

| Secret | Purpose | Where to Get It |
|--------|---------|-----------------|
| `TURNSTILE_SECRET_KEY` | Server-side Turnstile CAPTCHA verification | Cloudflare dashboard |
| `TURNSTILE_SITE_KEY` | Client-side Turnstile widget (GitHub secret only, baked at build time) | Cloudflare dashboard |
| `LEAD_API_KEY` | Identifies the agent site when calling the Lead API | Generate yourself |
| `LEAD_HMAC_SECRET` | Signs lead submission requests (prevents tampering/replay) | Generate yourself |
| `LEAD_API_URL` | .NET API endpoint for lead submissions | Known value |

## Step-by-Step Guide

### 1. Get Turnstile Keys

1. Go to [Cloudflare Dashboard](https://dash.cloudflare.com) → **Turnstile**
2. Click **Add site** (if you haven't already)
   - **Site name**: `Real Estate Star Agent Sites`
   - **Domain**: `real-estate-star.com`
   - **Widget Mode**: `Managed` (recommended) or `Invisible`
3. After creation, you'll see:
   - **Site Key** — public, used in the browser widget (`TURNSTILE_SITE_KEY`)
   - **Secret Key** — private, used for server-side verification (`TURNSTILE_SECRET_KEY`)
4. Copy both values

### 2. Generate Lead API Key

This is a random string that identifies the agent site to the .NET API. Generate one:

```bash
# Option A: OpenSSL
openssl rand -hex 32

# Option B: PowerShell
-join ((1..64) | ForEach-Object { '{0:x}' -f (Get-Random -Max 16) })

# Option C: Node.js
node -e "console.log(require('crypto').randomBytes(32).toString('hex'))"
```

This value goes in **both** places:
- `LEAD_API_KEY` (Cloudflare Worker secret + GitHub secret)
- `Hmac:ApiKeys:{agentId}` in the .NET API config (maps this key to a specific agent)

### 3. Generate HMAC Secret

This is a shared secret between the Cloudflare Worker and the .NET API for request signing:

```bash
# Same generation methods as above
openssl rand -hex 32
```

This value goes in **both** places:
- `LEAD_HMAC_SECRET` (Cloudflare Worker secret + GitHub secret)
- `Hmac:HmacSecret` in the .NET API config

### 4. Lead API URL

This is simply the API endpoint:
- **Production**: `https://api.real-estate-star.com`
- **Local dev**: `http://localhost:5135`

## Apply the Secrets

### Option A: Setup Script (recommended)

```powershell
# Set env vars first
$env:TURNSTILE_SECRET_KEY = "0x..."        # from step 1
$env:LEAD_API_KEY = "abc123..."            # from step 2
$env:LEAD_HMAC_SECRET = "def456..."        # from step 3
$env:LEAD_API_URL = "https://api.real-estate-star.com"

# Run the script
.\infra\cloudflare\set-worker-secrets.ps1 -FromEnv
```

Or run interactively (prompts for each value):
```powershell
.\infra\cloudflare\set-worker-secrets.ps1
```

### Option B: Deploy Script

The deploy script (`deploy-agent-site.ps1 -Production`) automatically injects secrets after deploy if the env vars are set.

### Option C: Manual via wrangler

```bash
cd apps/agent-site
echo '{"TURNSTILE_SECRET_KEY":"0x...","LEAD_API_KEY":"abc...","LEAD_HMAC_SECRET":"def...","LEAD_API_URL":"https://api.real-estate-star.com"}' | npx wrangler secret bulk
```

## Set GitHub Secrets

These same values must also be set as GitHub Actions secrets so CI deploys inject them:

```bash
gh secret set TURNSTILE_SECRET_KEY --body "0x..."
gh secret set TURNSTILE_SITE_KEY --body "0x..."      # site key, not secret key
gh secret set LEAD_API_KEY --body "abc..."
gh secret set LEAD_HMAC_SECRET --body "def..."
gh secret set LEAD_API_URL --body "https://api.real-estate-star.com"
```

## Verify

After setting secrets, verify the worker has them:

```bash
cd apps/agent-site
npx wrangler secret list
```

You should see all 4 secrets listed. Then test the lead form on production — the Turnstile check should pass.

## Match the API Side

The .NET API must recognize the same keys. In `appsettings.json` (or environment/Azure secrets):

```json
{
  "Hmac": {
    "ApiKeys": {
      "jenise-buckalew": "<same LEAD_API_KEY value>"
    },
    "HmacSecret": "<same LEAD_HMAC_SECRET value>"
  }
}
```
