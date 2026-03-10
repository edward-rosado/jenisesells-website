# Deployment-Ready Onboarding with Live Integrations — Design

> **Date:** 2026-03-10
> **Status:** Approved
> **Depends on:** Platform onboarding (PR #3, merged), CMA pipeline (merged)

## Problem

The onboarding flow is 82% built but all external integrations are stubs. For the demo to sell, agents need to see **their own inbox receive a CMA email**, **their own Drive get organized**, and **real payment checkout**. We also need it running locally for testing and on a free URL for sharing.

## Updated Onboarding State Machine

```
ScrapeProfile → ConfirmIdentity → CollectBranding → ConnectGoogle → GenerateSite → PreviewSite → DemoCma → ShowResults → CollectPayment → TrialActivated
```

New state: **ConnectGoogle** — inserted between CollectBranding and GenerateSite.

## Section 1: Google OAuth Integration

### Flow

1. Chat AI says: *"To show you the full experience, let's connect your Google account. This is how your CMA reports get emailed and organized."*
2. Chat renders a new `google_auth` card with a "Connect Google Account" button
3. Button opens popup to `GET /oauth/google/start?sessionId={id}`
4. API redirects to Google OAuth consent screen with 7 scopes
5. Google redirects back to `GET /oauth/google/callback?code=...&state={sessionId}`
6. API exchanges code for tokens, stores on session with 7-day TTL
7. API cross-validates Google profile (name, email) against scraped profile
8. Popup closes, chat receives confirmation, state advances

### Scopes (single consent)

| Scope | URI | Purpose |
|-------|-----|---------|
| userinfo.profile | `https://www.googleapis.com/auth/userinfo.profile` | Cross-validate name |
| userinfo.email | `https://www.googleapis.com/auth/userinfo.email` | Cross-validate email |
| gmail.send | `https://www.googleapis.com/auth/gmail.send` | Send CMA emails from agent's account |
| drive.file | `https://www.googleapis.com/auth/drive.file` | Create folder structure, upload PDFs |
| documents | `https://www.googleapis.com/auth/documents` | Create Lead Brief as Google Doc |
| spreadsheets | `https://www.googleapis.com/auth/spreadsheets` | Create/update lead tracking sheet |
| calendar.events | `https://www.googleapis.com/auth/calendar.events` | Future: scheduling |

### Token Storage

- Tokens stored on `OnboardingSession` as `GoogleTokens` record
- Fields: AccessToken, RefreshToken, ExpiresAt, Scopes, GoogleEmail, GoogleName
- 7-day TTL — `TrialExpiryService` deletes tokens + session data on expiry
- If agent converts → tokens migrate to permanent agent config (`config/agents/{id}.credentials.json`)
- Credentials never in git (`.gitignore` already excludes `*.credentials.json`)

### Cross-Validation

- Compare Google profile name vs scraped Zillow/Realtor name
- Compare Google email vs any email found in scrape
- Mismatches flagged to AI for reconciliation via `update_profile` tool
- Additional Google profile data (photo URL, locale) enriches the agent profile

### New Backend Components

- `Features/Onboarding/ConnectGoogle/StartGoogleOAuthEndpoint.cs` — `GET /oauth/google/start?sessionId={id}`
- `Features/Onboarding/ConnectGoogle/GoogleOAuthCallbackEndpoint.cs` — `GET /oauth/google/callback`
- `Features/Onboarding/Services/GoogleOAuthService.cs` — token exchange, profile fetch, refresh
- `Features/Onboarding/GoogleTokens.cs` — domain type at feature root
- `Features/Onboarding/Tools/GoogleAuthCardTool.cs` — returns OAuth URL for chat card

### New Frontend Component

- `components/chat/GoogleAuthCard.tsx` — "Connect Google Account" button
  - Opens popup window to OAuth start URL
  - Listens for `window.postMessage` from callback page
  - On success: sends confirmation message to chat, enabling state advance

## Section 2: Real Site Deployment + Preview

### Current State

`SiteDeployService` writes an agent config JSON and sets a fake URL. No real deploy.

### Target State

`DeploySiteTool` triggers a real deploy of the agent's white-label site:

1. Generates agent config JSON from scraped profile + branding + Google profile data
2. Writes config to `config/agents/{agent-slug}.json`
3. Deploys the `apps/agent-site/` Next.js app to Cloudflare Pages with agent-specific env vars
4. Returns a real preview URL: `https://{agent-slug}.real-estate-star.pages.dev`
5. Chat renders `site_preview` card with iframe showing the live site

### Deploy Strategy

**For MVP/demo:** Use Cloudflare Pages preview deployments via Wrangler CLI.

Each agent gets a preview deployment (not a separate project). The deploy command:
```bash
npx wrangler pages deploy apps/agent-site/.next \
  --project-name real-estate-star-agents \
  --branch {agent-slug} \
  --commit-message "Deploy {agent-name}'s site"
```

This produces a URL like `{agent-slug}.real-estate-star-agents.pages.dev`.

**For production:** Each paying agent gets their own Cloudflare Pages project + custom domain.

### New Backend Changes

- `SiteDeployService.DeployAsync()` — runs Wrangler CLI to deploy, returns live URL
- Needs `CLOUDFLARE_API_TOKEN` and `CLOUDFLARE_ACCOUNT_ID` env vars
- Agent config written before deploy so the site can read it at build time
- Timeout: 60s max (Pages deploys are fast)

### Demo Flow

1. AI: *"I'm building your website now..."*
2. Chat shows a progress indicator while deploy runs (~15-30s)
3. AI: *"Your site is live! Here's a preview."*
4. Chat renders `site_preview` iframe with the real URL
5. AI: *"See that CMA form at the bottom? Let me show you what happens when a seller fills it out..."*
6. → Transitions to DemoCma state

### Config Required

| Key | Location | Source |
|-----|----------|--------|
| `Cloudflare:ApiToken` | `appsettings.Development.json` | Cloudflare Dashboard → API Tokens |
| `Cloudflare:AccountId` | `appsettings.Development.json` | Cloudflare Dashboard → Account ID |

## Section 3: Real CMA Pipeline Integration (Triggered from Live Site)

### Current State

`SubmitCmaFormTool` returns a hardcoded success string.

### Target State

`SubmitCmaFormTool` invokes the real CMA pipeline, which:

1. Fetches comps from Zillow/Realtor/Redfin/Attom (parallel)
2. Researches the lead from public data
3. Calls Claude for market analysis
4. Generates branded PDF with QuestPDF
5. Creates Drive folder structure (all 6 top-level folders on first run)
6. Uploads CMA PDF to `1 - Leads/{Name}/{Address}/`
7. Creates Lead Brief as Google Doc in same folder
8. Creates/updates lead tracking Google Sheet
9. Sends email with CMA PDF — FROM the agent's own Gmail
10. Agent gets a Drive notification on their phone

### Drive Folder Structure (created on first CMA)

```
Real Estate Star/
├── 1 - Leads/
│   └── {Demo Lead Name}/
│       └── {Sample Address}/
│           ├── CMA_2026-03-10.pdf
│           ├── Lead-Brief.gdoc
│           └── Communications/
├── 2 - Active Clients/
├── 3 - Under Contract/
├── 4 - Closed/
├── 5 - Inactive/
│   ├── Dead Leads/
│   └── Expired Clients/
└── 6 - Referral Network/
    ├── Agents/
    ├── Brokerages/
    └── Summary/
        └── Referral-Fees-Tracker.gsheet
```

All 6 top-level folders created on first run. Only `1 - Leads/` populated with demo data.

### Demo vs Production Behavior

| Aspect | Production (Seller submits) | Onboarding Demo (Agent tests) |
|--------|---------------------------|-------------------------------|
| Who submits form | Real lead / homeowner | AI pre-fills with sample address |
| Email recipient | Lead's email | Agent's own email (sees seller experience) |
| Lead Brief | Created silently in Drive | AI walks agent through it in chat |
| Drive organization | Auto-files silently | AI says "Check your Drive" |
| Sheets logging | Row added silently | AI shows the tracking spreadsheet |

### AI Narration After CMA Completes

> "Check your Drive — I've created your lead management system and filed the first CMA. Check your inbox — that's what your sellers will receive. And check your Sheets — every lead is tracked automatically."

### Wiring

- `SubmitCmaFormTool.ExecuteAsync()` directly invokes `CmaPipeline` (in-process, not HTTP)
- Passes session's `GoogleTokens` so `GwsService` authenticates as the agent
- Demo mode flag controls: email recipient = agent, narration = verbose
- `GwsService` needs a `SetCredentials(GoogleTokens)` method for per-request auth

## Section 4: Stripe Checkout (Test Mode)

### Flow

1. After CMA demo + ShowResults, state advances to `CollectPayment`
2. AI presents value prop and chat renders `payment_card`
3. "Start Free Trial" button calls `create_stripe_session` tool
4. API creates Stripe Checkout Session → returns URL
5. User redirects to Stripe's hosted checkout (test mode)
6. Stripe redirects back to `/onboard?payment=success&session_id={id}`
7. Webhook `POST /webhooks/stripe` confirms → state advances to TrialActivated

### Stripe Configuration

- Account in **test mode** (no real charges)
- Product: "Real Estate Star Platform" — $900 one-time
- Checkout Session with `payment_intent_data.setup_future_usage = "off_session"`
- Test card: `4242 4242 4242 4242`, any future date, any CVC

### New Backend Components

- `Features/Onboarding/Services/StripeService.cs` — replace stub with Stripe.net SDK
- `Features/Onboarding/Webhooks/StripeWebhookEndpoint.cs` — `POST /webhooks/stripe`
- Config keys: `Stripe:SecretKey`, `Stripe:WebhookSecret`, `Stripe:PriceId`

### Frontend Changes

- `PaymentCard.tsx` — "Start Free Trial" opens Stripe Checkout URL (redirect, not embedded)
- `NEXT_PUBLIC_STRIPE_KEY` env var for publishable key
- `/onboard` page handles `?payment=success` query param on return

## Section 5: Local Development Setup

### Two Processes

```bash
# Terminal 1: .NET API on :5000
cd apps/api/RealEstateStar.Api
dotnet run

# Terminal 2: Next.js Platform on :3000
cd apps/platform
npm run dev
```

### Required Configuration

| Key | Location | Source |
|-----|----------|--------|
| `Anthropic:ApiKey` | `appsettings.Development.json` | console.anthropic.com |
| `Attom:ApiKey` | `appsettings.Development.json` | attomdata.com |
| `Stripe:SecretKey` | `appsettings.Development.json` | Stripe Dashboard |
| `Stripe:WebhookSecret` | `appsettings.Development.json` | `stripe listen` CLI |
| `Stripe:PriceId` | `appsettings.Development.json` | Stripe Dashboard |
| `Google:ClientId` | `appsettings.Development.json` | Google Cloud Console |
| `Google:ClientSecret` | `appsettings.Development.json` | Google Cloud Console |
| `Cloudflare:ApiToken` | `appsettings.Development.json` | Cloudflare Dashboard |
| `Cloudflare:AccountId` | `appsettings.Development.json` | Cloudflare Dashboard |
| `NEXT_PUBLIC_API_URL` | `apps/platform/.env.local` | `http://localhost:5000` |
| `NEXT_PUBLIC_STRIPE_KEY` | `apps/platform/.env.local` | Stripe `pk_test_...` |

### Google OAuth Redirect URI (local)

`http://localhost:5000/oauth/google/callback`

### CORS

Already configured for `localhost:3000` in `Program.cs`.

## Section 6: Free Deployment (Phase 2)

### Frontend: Cloudflare Pages (free tier)

- Zero egress, unlimited bandwidth, 300+ edge locations
- Deploy from GitHub via Cloudflare dashboard
- URL: `real-estate-star.pages.dev`
- Build command: `npm run build` in `apps/platform/`

### API: Railway (free tier — $5 credit/month, no credit card required)

- Dockerfile deploy from GitHub
- URL: `real-estate-star-api.up.railway.app`
- Environment variables set in Railway dashboard

### Production OAuth Redirect URI

`https://real-estate-star-api.up.railway.app/oauth/google/callback`

## Section 7: Account Setup Checklist

### Step 1: Anthropic API Key (~2 min)

1. Go to console.anthropic.com
2. Sign in or create account
3. Settings → API Keys → Create Key
4. Copy the key

### Step 2: Stripe Account (~3 min)

1. Go to dashboard.stripe.com/register
2. Create account (skip business verification — test mode doesn't need it)
3. Dashboard → Developers → API Keys
4. Copy Publishable key (`pk_test_...`) and Secret key (`sk_test_...`)
5. Create Product: "Real Estate Star Platform", $900 one-time
6. Copy the Price ID (`price_...`)

### Step 3: Google Cloud OAuth (~5 min)

1. Go to console.cloud.google.com
2. Create project: "Real Estate Star"
3. Enable APIs: Gmail, Drive, Docs, Sheets, Calendar
4. APIs & Services → OAuth consent screen → External → Create
5. App name: "Real Estate Star", support email: yours
6. Scopes: add all 7 listed above
7. Test users: add your email
8. Credentials → Create OAuth 2.0 Client ID → Web application
9. Authorized redirect URI: `http://localhost:5000/oauth/google/callback`
10. Copy Client ID and Client Secret

### Step 4: Cloudflare Account (~3 min)

1. Go to dash.cloudflare.com — sign up or sign in
2. Copy Account ID from the dashboard sidebar
3. Go to My Profile → API Tokens → Create Token
4. Use "Edit Cloudflare Pages" template
5. Copy the API token
6. Create a Pages project: `real-estate-star-agents` (can be empty initially)

**Total setup time: ~15 minutes**

## Implementation Scope

| Component | Status | Effort |
|-----------|--------|--------|
| `SiteDeployService.cs` — real Cloudflare Pages deploy via Wrangler | Modify | Medium |
| `DeploySiteTool` — trigger real deploy, return live URL | Modify | Small |
| `ConnectGoogle/` endpoints (start + callback) | New | Medium |
| `GoogleOAuthService.cs` | New | Medium |
| `GoogleTokens.cs` domain type | New | Small |
| `GoogleAuthCard.tsx` chat component | New | Small |
| `GoogleAuthCardTool` tool | New | Small |
| `OnboardingState.cs` — add ConnectGoogle | Modify | Small |
| `OnboardingStateMachine.cs` — add transitions + tools | Modify | Small |
| `OnboardingChatService.cs` — add system prompt | Modify | Small |
| `SubmitCmaFormTool` — wire to real pipeline | Modify | Medium |
| `GwsService` — per-request Google token auth | Modify | Medium |
| `StripeService.cs` — real Stripe.net SDK | Modify | Medium |
| `StripeWebhookEndpoint.cs` | New | Medium |
| `PaymentCard.tsx` — Stripe redirect | Modify | Small |
| `MessageRenderer.tsx` — add google_auth type | Modify | Small |
| `.env.local.example` — all env vars | Modify | Small |
| Tests for all new components | New | Medium |
| Drive folder structure creation in GwsService | Modify | Medium |

## Key Design Decisions

1. **Popup OAuth (not redirect)** — keeps chat context alive, no page navigation
2. **Stripe Checkout (not Elements)** — hosted page = PCI compliant with zero frontend work
3. **In-process CMA pipeline call** — no HTTP round-trip for demo, direct DI injection
4. **drive.file scope (not full drive)** — only access files WE create, builds trust
5. **7-day token TTL** — natural conversion pressure, clean data on non-conversion
6. **Railway for API** — free tier, no credit card, Docker-native, auto-deploy from GitHub
7. **All 6 Drive folders created on first CMA** — even empty ones, shows the full system
