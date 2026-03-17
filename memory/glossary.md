# Glossary

## Acronyms
| Term | Meaning | Context |
|------|---------|---------|
| CMA | Comparative Market Analysis | Core feature — generates market reports for agents |
| GA4 | Google Analytics 4 | Agent site tracking (not used on platform) |
| GTM | Google Tag Manager | Agent site tracking container |
| OTLP | OpenTelemetry Protocol | Backend traces/metrics export |

## Internal Terms
| Term | Meaning |
|------|---------|
| platform | The main Real Estate Star admin portal at apps/portal |
| agent site | White-label websites for real estate agents at apps/agent-site |
| agent config | JSON config file per tenant at config/agents/{id}.json |
| reference tenant | jenise-buckalew — the first agent, used for testing |

## Infrastructure Decisions
| Decision | Choice | Why |
|----------|--------|-----|
| Hosting | Cloudflare Pages + Workers | Cost, performance, already using CF for DNS |
| Analytics (platform) | Cloudflare Web Analytics | Free, cookie-free, works natively with CF hosting |
| Analytics (agent sites) | GA4/GTM/Meta Pixel per agent | Agents need ad conversion tracking |
| Backend observability | OpenTelemetry → Grafana Cloud | Industry standard, vendor neutral |
| Error tracking | Sentry | Session replay on errors, source maps |
