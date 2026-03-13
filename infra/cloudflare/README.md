# Cloudflare DNS & Domain Configuration

This guide covers DNS records, custom domain setup, and Cloudflare configuration for the Real Estate Star platform.

## 1. Required DNS Records

All domains use Cloudflare as the DNS provider. The primary domain is `real-estate-star.com` (or your chosen domain).

| Type | Name | Target | Proxy | Purpose |
|------|------|--------|-------|---------|
| CNAME | `platform` | `real-estate-star-platform.pages.dev` | Proxied (orange cloud) | Platform app (portal) |
| CNAME | `*.agents` | `real-estate-star-agent-site.pages.dev` | Proxied (orange cloud) | White-label agent sites (wildcard) |
| CNAME | `api` | `real-estate-star-api.<region>.azurecontainerapps.io` | Proxied (orange cloud) | Backend API |
| CNAME | `www` | `platform.real-estate-star.com` | Proxied (orange cloud) | Redirect to platform |

This gives you:
- `platform.real-estate-star.com` -- the Real Estate Star admin portal
- `<agent-slug>.agents.real-estate-star.com` -- each agent's white-label site
- `api.real-estate-star.com` -- the backend API
- `www.real-estate-star.com` -- redirects to platform

## 2. Cloudflare Pages Custom Domain Setup

### Platform App

1. In Cloudflare dashboard, go to **Workers & Pages > real-estate-star-platform**
2. Click **Custom domains > Set up a custom domain**
3. Enter `platform.real-estate-star.com`
4. Cloudflare auto-creates the CNAME record if the domain is on Cloudflare DNS
5. Wait for SSL certificate provisioning (usually < 5 minutes)

### Agent Site (Wildcard)

1. Go to **Workers & Pages > real-estate-star-agent-site**
2. Click **Custom domains > Set up a custom domain**
3. Enter `*.agents.real-estate-star.com`
4. Cloudflare requires an **Advanced Certificate Manager** subscription ($10/mo) for wildcard custom domains on Pages
5. Alternatively, add individual agent domains (e.g., `jenise.agents.real-estate-star.com`) without the wildcard -- this works on the free plan but requires manual setup per agent

### Custom Vanity Domains (per agent)

Agents can bring their own domains (e.g., `jenisesellsnj.com`):

1. Agent adds their domain to Cloudflare (free plan is fine)
2. Agent creates a CNAME: `@` -> `real-estate-star-agent-site.pages.dev`
3. Add the domain in **Workers & Pages > real-estate-star-agent-site > Custom domains**
4. Update the agent's config in `config/agents/<agent-id>.json` with the custom domain

## 3. Azure Container Apps Custom Domain Setup

The API runs on Azure Container Apps. To add a custom domain:

```bash
# 1. Add the custom domain to the Container App Environment
az containerapp env certificate upload \
  --name real-estate-star-env \
  --resource-group real-estate-star-rg \
  --certificate-file /path/to/cert.pem \
  --certificate-password ""

# 2. Bind the domain to the container app
az containerapp hostname add \
  --name real-estate-star-api \
  --resource-group real-estate-star-rg \
  --hostname api.real-estate-star.com

# 3. Bind the managed certificate
az containerapp hostname bind \
  --name real-estate-star-api \
  --resource-group real-estate-star-rg \
  --hostname api.real-estate-star.com \
  --environment real-estate-star-env \
  --validation-method CNAME
```

Since Cloudflare is proxying, you need to temporarily set the CNAME to DNS-only (grey cloud) during domain validation, then switch back to proxied after validation completes.

Alternatively, use Cloudflare Origin Certificates:

1. In Cloudflare, go to **SSL/TLS > Origin Server > Create Certificate**
2. Generate a certificate for `api.real-estate-star.com`
3. Upload to Azure Container Apps as shown above
4. Set SSL mode to **Full (strict)** (see below)

## 4. SSL/TLS Settings

In Cloudflare dashboard, go to **SSL/TLS**:

| Setting | Value | Reason |
|---------|-------|--------|
| SSL/TLS encryption mode | **Full (strict)** | Azure Container Apps and Cloudflare Pages both have valid certs |
| Always Use HTTPS | **On** | Force all traffic to HTTPS |
| Minimum TLS Version | **TLS 1.2** | Security baseline |
| Automatic HTTPS Rewrites | **On** | Fix mixed content |
| HTTP Strict Transport Security (HSTS) | **On** | Force browsers to use HTTPS |
| HSTS Max-Age | **6 months** (15768000) | Standard production value |
| HSTS Include Subdomains | **On** | Covers all subdomains |

## 5. Page Rules & Cache Configuration

Create these page rules (or use Cache Rules in the new dashboard):

### Bypass Cache for API

**URL pattern:** `api.real-estate-star.com/*`

| Setting | Value |
|---------|-------|
| Cache Level | Bypass |
| Security Level | High |

This ensures API responses are never cached by Cloudflare's edge, which would break session state, SSE streaming, and authenticated endpoints.

### Bypass Cache for Platform Auth Routes

**URL pattern:** `platform.real-estate-star.com/api/*`

| Setting | Value |
|---------|-------|
| Cache Level | Bypass |

Next.js API routes and server actions must not be edge-cached.

### Cache Static Assets Aggressively

**URL pattern:** `*.agents.real-estate-star.com/_next/static/*`

| Setting | Value |
|---------|-------|
| Cache Level | Cache Everything |
| Edge Cache TTL | 1 month |
| Browser Cache TTL | 1 year |

Next.js static assets are content-hashed and safe to cache indefinitely.

## 6. Security Settings

In Cloudflare dashboard:

| Setting | Location | Value |
|---------|----------|-------|
| Bot Fight Mode | Security > Bots | **On** |
| Browser Integrity Check | Security > Settings | **On** |
| Email Address Obfuscation | Scrape Shield | **On** |
| Hotlink Protection | Scrape Shield | **On** |
| WAF Managed Rules | Security > WAF | **On** (free tier includes OWASP core) |

## 7. Firewall Rules (Recommended)

Create these custom firewall rules:

| Rule | Expression | Action |
|------|------------|--------|
| Block non-US traffic to API | `http.host eq "api.real-estate-star.com" and ip.geoip.country ne "US"` | Block |
| Challenge suspicious bots | `cf.threat_score gt 30` | Managed Challenge |
| Allow health checks | `http.request.uri.path contains "/health"` | Allow |

Adjust the geo-blocking rule based on where your agents operate.

## 8. DNS Propagation Verification

After setting up DNS records, verify propagation:

```bash
# Check CNAME resolution
dig platform.real-estate-star.com CNAME +short
dig api.real-estate-star.com CNAME +short

# Check SSL
curl -vI https://platform.real-estate-star.com 2>&1 | grep "SSL certificate"
curl -vI https://api.real-estate-star.com 2>&1 | grep "SSL certificate"

# Check API health through Cloudflare
curl https://api.real-estate-star.com/health/live
curl https://api.real-estate-star.com/health/ready
```
