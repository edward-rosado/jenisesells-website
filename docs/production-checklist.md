# Production Readiness Checklist

Use this checklist before the first production deployment and before each major release.

---

## Infrastructure (Azure Container Apps)

- [ ] Azure Container Apps environment created in target region
- [ ] Container image built and pushed to Azure Container Registry (ACR)
- [x] Dockerfile uses non-root user (`appuser`, UID 1001)
- [x] Dockerfile pins base image versions (no `:latest`)
- [x] Health check configured: `GET /health/live` (liveness), `GET /health/ready` (readiness)
- [ ] Min replicas set to 1, max replicas set based on expected load
- [ ] CPU/memory limits configured (recommend 1 vCPU / 2 GiB minimum)
- [x] Ingress configured for HTTPS on port 8080

### Secrets & Environment Variables

- [ ] `Anthropic__ApiKey` set as secret
- [ ] `Attom__ApiKey` set as secret
- [ ] `Google__ClientId` set as env var
- [ ] `Google__ClientSecret` set as secret
- [ ] `Google__RedirectUri` set to production callback URL
- [ ] `Stripe__SecretKey` set as secret (live key, not test key)
- [ ] `Stripe__WebhookSecret` set as secret (from Stripe dashboard)
- [ ] `Stripe__PriceId` set to production price ID
- [ ] `ScraperApi__ApiKey` set as secret
- [ ] `Cloudflare__ApiToken` set as secret
- [ ] `Cloudflare__AccountId` set as secret
- [ ] `Platform__BaseUrl` set to `https://platform.real-estate-star.com`
- [ ] `Otel__Endpoint` set to Grafana Cloud OTLP gateway URL
- [ ] `OTEL_EXPORTER_OTLP_HEADERS` set with Grafana Cloud auth header
- [ ] `Cors__AllowedOrigins__0` set to `https://platform.real-estate-star.com`
- [ ] `AllowedHosts` set to production domain(s) (not `localhost`, not `*`)
- [ ] No test/dev API keys present in production config
- [ ] All secrets use Azure Container Apps secrets (not plain env vars)

> **Known issue**: `appsettings.json` currently has `AllowedHosts: "*"` — must override via env var in production.

### Docker

- [ ] Production image scanned for vulnerabilities (Trivy, Snyk, or ACR scan)
- [x] Image size optimized (multi-stage build, no SDK in runtime image)
- [x] No development credentials baked into the image
- [ ] `.dockerignore` excludes test projects, docs, config/agents

---

## DNS & Domains

See `infra/cloudflare/README.md` for detailed setup steps.

- [x] `platform.real-estate-star.com` CNAME pointing to Cloudflare Workers
- [ ] `api.real-estate-star.com` CNAME pointing to Azure Container Apps
- [x] Individual agent CNAMEs: `{handle}.real-estate-star.com` pointing to Cloudflare Workers (no wildcard DNS — each agent registered via `infra/cloudflare/add-agent-domain.ps1`)
- [x] `www.real-estate-star.com` CNAME redirecting to platform
- [x] SSL/TLS mode set to **Full (strict)** in Cloudflare
- [x] HSTS enabled with includeSubDomains
- [x] Always Use HTTPS enabled
- [x] Minimum TLS version set to 1.2
- [ ] API cache bypass page rule active
- [x] DNS propagation verified with `dig` and `curl`
- [x] All endpoints respond with valid SSL certificates

---

## Monitoring (Grafana Cloud)

See `infra/grafana/README.md` for detailed setup steps.

> **Status**: OpenTelemetry instrumentation exists in the API (ActivitySource, Meter, structured logging), but Grafana Cloud is NOT yet configured as a destination. Traces and metrics are instrumented but have no exporter endpoint.

- [ ] Grafana Cloud free tier account created
- [ ] OTLP connection configured with API token
- [ ] `Otel__Endpoint` and `OTEL_EXPORTER_OTLP_HEADERS` set on Azure Container Apps
- [ ] Traces appearing in Grafana Cloud Traces explorer
- [ ] Metrics appearing in Grafana Cloud Metrics explorer
- [ ] ASP.NET Core dashboard imported (ID: 19924)
- [ ] .NET Runtime dashboard imported (ID: 19925)
- [ ] Custom dashboard created for CMA and onboarding metrics

### Alerts Configured

- [ ] High error rate: 5xx rate > 5% for 5 min (Critical)
- [ ] CMA pipeline failures: `cma.failed` rate > 3/hour (Warning)
- [ ] Slow CMA pipeline: `cma.duration` p95 > 120s for 10 min (Warning)
- [ ] Health check down: `/health/ready` non-200 for 2 min (Critical)
- [ ] High latency: `http.server.request.duration` p99 > 5s for 5 min (Warning)
- [ ] Alert notification channel configured (email, Slack, or PagerDuty)

---

## Stripe (Payments)

- [ ] Stripe account in **live mode** (not test mode)
- [ ] Live secret key (`sk_live_...`) set as Azure secret
- [ ] Production product and price created in Stripe dashboard
- [ ] `Stripe__PriceId` set to live price ID
- [ ] Webhook endpoint registered in Stripe dashboard
  - URL: `https://api.real-estate-star.com/webhooks/stripe`
  - Events: `checkout.session.completed`
- [ ] Webhook signing secret (`whsec_...`) set as Azure secret
- [x] Webhook endpoint excluded from rate limiting
- [ ] Test a complete checkout flow end-to-end
- [ ] Verify webhook delivery in Stripe dashboard (Events > Webhooks)
- [x] Idempotency: duplicate webhook events do not create duplicate state transitions
- [ ] Customer portal link configured for subscription management

---

## Google OAuth

- [ ] Google Cloud project created (or existing project configured)
- [ ] OAuth consent screen configured
  - App name: Real Estate Star
  - User support email set
  - Authorized domains: `real-estate-star.com`
  - Scopes: `email`, `profile`, `openid`
- [ ] OAuth consent screen **published** (not in test mode)
  - If restricted scopes: submit for Google verification
- [ ] OAuth 2.0 Client ID created (Web application type)
- [ ] Authorized redirect URI set to: `https://api.real-estate-star.com/oauth/google/callback`
- [ ] `Google__ClientId` and `Google__ClientSecret` set on Azure
- [ ] `Google__RedirectUri` set to production callback URL
- [ ] Test full OAuth flow: login, consent, callback, profile extraction
- [x] Token storage has TTL with cleanup (not unbounded)
- [x] `state` parameter uses cryptographic nonce (CSRF protection)
- [x] Nonce is single-use — cleared after validation

---

## Security

### Repository & CI/CD

- [ ] Branch protection enabled on `main`
  - Require pull request reviews (1+ approver)
  - Require status checks to pass
  - Require linear history (no merge commits)
- [x] GitHub Actions secrets configured (not hardcoded in workflows)
- [x] No API keys, tokens, or credentials committed to the repository
- [x] `.gitignore` includes `appsettings.Development.json`, `.env.local`
- [ ] `.gitignore` includes `*.pfx`, `*.p12` (certificate files)
- [ ] Dependency scanning enabled (Dependabot or Snyk)

### API Security

- [x] CORS `AllowedOrigins` restricted to production domain(s) only
- [x] Rate limiting active: global (100/min/IP), session creation (5/hr/IP), CMA (10/hr/agent), chat (20/min/session)
- [x] Security headers present on all responses:
  - `X-Content-Type-Options: nosniff`
  - `X-Frame-Options: DENY`
  - `Referrer-Policy: strict-origin-when-cross-origin`
  - `Strict-Transport-Security: max-age=31536000; includeSubDomains`
- [x] Bearer token authentication on session-scoped endpoints
- [x] ForwardedHeaders middleware configured for Azure proxy (before rate limiter)
- [ ] No PII in telemetry spans or structured logs (audit: lead names/emails still logged in CmaPipeline warning logs)
- [x] Correlation ID validated (length <= 64, charset restricted)
- [x] Error responses use ProblemDetails, not raw exception messages

### Key Rotation Plan

- [ ] Document rotation procedure for each secret:
  - Anthropic API key: regenerate in Anthropic dashboard, update Azure secret
  - Stripe keys: roll in Stripe dashboard, update Azure secrets
  - Google OAuth secret: regenerate in Google Cloud Console, update Azure secret
  - Cloudflare API token: regenerate in Cloudflare dashboard, update Azure secret
  - ScraperAPI key: regenerate in ScraperAPI dashboard, update Azure secret
  - Grafana OTLP token: regenerate in Grafana Cloud, update Azure env var
- [ ] Schedule quarterly key rotation reminders

---

## Smoke Tests

Run these after every production deployment:

### Health Endpoints

```bash
# Liveness (should return 200 immediately)
curl -sf https://api.real-estate-star.com/health/live

# Readiness (should return 200 with dependency status)
curl -sf https://api.real-estate-star.com/health/ready | jq .
```

### Onboarding Flow

```bash
# Create a session
curl -sf -X POST https://api.real-estate-star.com/onboarding/sessions \
  -H "Content-Type: application/json" \
  -d '{"profileUrl": "https://www.zillow.com/profile/test-agent"}' \
  | jq '.sessionId'

# Verify session was created (use sessionId from above)
# Send a chat message and verify streaming response
```

### CMA Pipeline

```bash
# Submit a CMA job (requires valid agent config)
curl -sf -X POST https://api.real-estate-star.com/agents/jenise-buckalew/cma \
  -H "Content-Type: application/json" \
  -d '{
    "address": "123 Test St",
    "city": "Cherry Hill",
    "state": "NJ",
    "zip": "08003",
    "propertyType": "SingleFamily"
  }'
```

### Agent Site

```bash
# Verify agent site loads
curl -sf https://jenise-buckalew.real-estate-star.com/ | head -20

# Verify static assets load
curl -sfI https://jenise-buckalew.real-estate-star.com/_next/static/ \
  | grep "cache-control"
```

### Platform

```bash
# Verify platform loads
curl -sf https://platform.real-estate-star.com/ | head -20

# Verify onboarding page loads
curl -sf https://platform.real-estate-star.com/onboard | head -20
```

### Security Headers

```bash
# Verify security headers on API
curl -sI https://api.real-estate-star.com/health/live | grep -iE "x-content-type|x-frame|referrer-policy|strict-transport"
```

---

## Post-Deploy Verification

After all smoke tests pass:

- [ ] Monitor Grafana dashboards for 30 minutes post-deploy
- [ ] Check for elevated error rates in traces
- [ ] Verify no 5xx errors in Cloudflare analytics
- [ ] Test onboarding flow end-to-end in browser
- [ ] Test CMA submission end-to-end in browser
- [ ] Verify Stripe webhook delivery for a test purchase
- [ ] Confirm alert notifications fire correctly (trigger a test alert)
