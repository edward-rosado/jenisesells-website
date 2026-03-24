# Memory

## Me
Eddie Rosado, Senior Manager of Engineering at Mindbody/Playlist. Building Real Estate Star as a side project — a SaaS platform that automates real estate agent workflows.

## Terms
| Term | Meaning |
|------|---------|
| platform | The main Real Estate Star admin portal (apps/portal) |
| agent site | White-label websites for real estate agents (apps/agent-site) |
| CMA | Comparative Market Analysis |
| lead | Contact information (name, email, phone, property, etc.) submitted by a buyer |

## Infrastructure
| Component | Details |
|-----------|---------|
| **Hosting** | Cloudflare Pages + Workers (via OpenNext) — NOT Vercel |
| **DNS** | Cloudflare, domain: real-estate-star.com (hyphenated) |
| **API** | .NET 10, 21 isolated projects, hosted on Azure Container Apps, proxied through Cloudflare |
| **Platform URL** | https://platform.real-estate-star.com |
| **Agent Sites URL** | https://{handle}.real-estate-star.com |
| **API URL** | https://api.real-estate-star.com |
| **Backend Observability** | OpenTelemetry → Grafana Cloud |
| **Error Tracking** | Sentry (agent-site only) |
| **Frontend Analytics** | Cloudflare Web Analytics — enable via CF dashboard, no code needed (auto-injected for proxied sites) |
| **CI/CD** | GitHub Actions → Cloudflare wrangler deploy |
| **Lead Storage** | Google Drive (prod) or Local (dev) via `IFileStorageProvider` abstraction (defined in Domain, implemented in Data, orchestrated by DataServices) |
| **Google API Clients** | Gmail, Drive, Docs, Sheets — per-agent OAuth via `IOAuthRefresher`; tokens persisted in Azure Table (`ITokenStore`) |
| **Token Store** | Azure Table Storage (`AzureTableTokenStore` in `Clients.Azure`) implements `ITokenStore` (Domain); DPAPI-encrypted tokens with ETag optimistic locking |
| **Anthropic Client** | Shared `IAnthropicClient` (Domain) → `AnthropicClient` (`Clients.Anthropic`); replaces 6 inline callers with a single injected abstraction |

## Preferences
- Use eddie-voice skill when talking
- Keep things simple, iterate later
- Cloudflare for everything hosting-related
