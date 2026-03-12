# Cloudflare DNS & Domain Configuration

This guide covers DNS records, custom domain setup, and Cloudflare configuration for the Real Estate Star platform.

## 1. Required DNS Records

All domains use Cloudflare as the DNS provider. The primary domain is `realestatestar.com` (or your chosen domain).

| Type | Name | Target | Proxy | Purpose |
|------|------|--------|-------|---------|
| CNAME | `platform` | `realestatestar-platform.pages.dev` | Proxied (orange cloud) | Platform app (portal) |
| CNAME | `*.agents` | `realestatestar-agent-site.pages.dev` | Proxied (orange cloud) | White-label agent sites (wildcard) |
| CNAME | `api` | `realestatestar-api.<region>.azurecontainerapps.io` | Proxied (orange cloud) | Backend API |
| CNAME | `www` | `platform.realestatestar.com` | Proxied (orange cloud) | Redirect to platform |

This gives you:
- `platform.realestatestar.com` -- the Real Estate Star admin portal
- `<agent-slug>.agents.realestatestar.com` -- each agent's white-label site
- `api.realestatestar.com` -- the backend API
- `www.realestatestar.com` -- redirects to platform

## 2. Cloudflare Pages Custom Domain Setup

### Platform App

1. In Cloudflare dashboard, go to **Workers & Pages > realestatestar-platform**
2. Click **Custom domains > Set up a custom domain**
3. Enter `platform.realestatestar.com`
4. Cloudflare auto-creates the CNAME record if the domain is on Cloudflare DNS
5. Wait for SSL certificate provisioning (usually < 5 minutes)

### Agent Site (Wildcard)

1. Go to **Workers & Pages > realestatestar-agent-site**
2. Click **Custom domains > Set up a custom domain**
3. Enter `*.agents.realestatestar.com`
4. Cloudflare requires an **Advanced Certificate Manager** subscription ($10/mo) for wildcard custom domains on Pages
5. Alternatively, add individual agent domains (e.g., `jenise.agents.realestatestar.com`) without the wildcard -- this works on the free plan but requires manual setup per agent

### Custom Vanity Domains (per agent)

Agents can bring their own domains (e.g., `jenisesellsnj.com`):

1. Agent adds their domain to Cloudflare (free plan is fine)
2. Agent creates a CNAME: `@` -> `realestatestar-agent-site.pages.dev`
3. Add the domain in **Workers & Pages > realestatestar-agent-site > Custom domains**
4. Update the agent's config in `config/agents/<agent-id>.json` with the custom domain

## 3. Azure Container Apps Custom Domain Setup

The API runs on Azure Container Apps. To add a custom domain:

```bash
# 1. Add the custom domain to the Container App Environment
az containerapp env certificate upload \
  --name realestatestar-env \
  --resource-group realestatestar-rg \
  --certificate-file /path/to/cert.pem \
  --certificate-password ""

# 2. Bind the domain to the container app
az containerapp hostname add \
  --name realestatestar-api \
  --resource-group realestatestar-rg \
  --hostname api.realestatestar.com

# 3. Bind the managed certificate
az containerapp hostname bind \
  --name realestatestar-api \
  --resource-group realestatestar-rg \
  --hostname api.realestatestar.com \
  --environment realestatestar-env \
  --validation-method CNAME
```

Since Cloudflare is proxying, you need to temporarily set the CNAME to DNS-only (grey cloud) during domain validation, then switch back to proxied after validation completes.

Alternatively, use Cloudflare Origin Certificates:

1. In Cloudflare, go to **SSL/TLS > Origin Server > Create Certificate**
2. Generate a certificate for `api.realestatestar.com`
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

**URL pattern:** `api.realestatestar.com/*`

| Setting | Value |
|---------|-------|
| Cache Level | Bypass |
| Security Level | High |

This ensures API responses are never cached by Cloudflare's edge, which would break session state, SSE streaming, and authenticated endpoints.

### Bypass Cache for Platform Auth Routes

**URL pattern:** `platform.realestatestar.com/api/*`

| Setting | Value |
|---------|-------|
| Cache Level | Bypass |

Next.js API routes and server actions must not be edge-cached.

### Cache Static Assets Aggressively

**URL pattern:** `*.agents.realestatestar.com/_next/static/*`

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
| Block non-US traffic to API | `http.host eq "api.realestatestar.com" and ip.geoip.country ne "US"` | Block |
| Challenge suspicious bots | `cf.threat_score gt 30` | Managed Challenge |
| Allow health checks | `http.request.uri.path contains "/health"` | Allow |

Adjust the geo-blocking rule based on where your agents operate.

## 8. DNS Propagation Verification

After setting up DNS records, verify propagation:

```bash
# Check CNAME resolution
dig platform.realestatestar.com CNAME +short
dig api.realestatestar.com CNAME +short

# Check SSL
curl -vI https://platform.realestatestar.com 2>&1 | grep "SSL certificate"
curl -vI https://api.realestatestar.com 2>&1 | grep "SSL certificate"

# Check API health through Cloudflare
curl https://api.realestatestar.com/health/live
curl https://api.realestatestar.com/health/ready
```
