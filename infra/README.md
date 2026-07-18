# Infrastructure

Scripts and templates for provisioning and managing Real Estate Star's cloud infrastructure.

## Directory Structure

```
infra/
├── azure/                          # Azure resource provisioning
│   ├── setup.sh                    # One-time: create resource group, Container App, ACR
│   ├── create-gh-credentials.sh    # One-time: create GitHub Actions service principal
│   ├── set-secrets.sh              # Set Container App secrets (interactive)
│   ├── provision-keyvault.ps1      # One-time: Key Vault + DPAPI key ring
│   ├── setup-whatsapp-storage.ps1  # One-time: WhatsApp queue + table storage
│   └── diagnose-api.ps1            # Debug: diagnose Container App deployment issues
├── cloudflare/                     # Cloudflare Workers deployment
│   ├── deploy-agent-site.ps1       # Deploy agent-site to Cloudflare Workers
│   ├── deploy-platform.ps1         # Deploy platform to Cloudflare Workers
│   ├── add-agent-domain.ps1        # Add subdomain for new agent
│   ├── set-worker-secrets.ps1      # Set secrets on Cloudflare Workers (PowerShell)
│   ├── set-worker-secrets.sh       # Set secrets on Cloudflare Workers (bash)
│   └── README.md                   # Cloudflare-specific docs
├── functions/                      # Azure Durable Functions
│   ├── main-consumption.bicep      # Y1 Consumption plan Bicep (current)
│   ├── migrate-to-consumption.sh   # Migration: Flex → Y1 Consumption
│   ├── deploy.sh                   # Provision Functions infra via Bicep
│   └── README.md                   # Functions-specific docs
├── grafana/                        # Monitoring dashboards
│   ├── real-estate-star-api-dashboard.json
│   ├── real-estate-star-functions-dashboard.json
│   └── README.md
├── go-live.sh                      # Interactive go-live runbook
├── verify-live.sh                  # Post-deployment verification
├── MIGRATION-COST-REDUCTION.md     # Cost reduction migration plan
└── README.md                       # This file
```

## CI/CD Workflows

Day-to-day deployments are handled by GitHub Actions, not these scripts:

| Workflow | What It Deploys |
|----------|----------------|
| `.github/workflows/deploy-api.yml` | API → Azure Container Apps (Docker) |
| `.github/workflows/deploy-functions.yml` | Functions → Azure Durable Functions |
| `.github/workflows/deploy-agent-site.yml` | Agent site → Cloudflare Workers |
| `.github/workflows/deploy-platform.yml` | Platform → Cloudflare Workers |

## When to Use These Scripts

- **First-time setup:** `azure/setup.sh` → `azure/create-gh-credentials.sh` → `azure/provision-keyvault.ps1`
- **Add WhatsApp:** `azure/setup-whatsapp-storage.ps1`
- **Add agent subdomain:** `cloudflare/add-agent-domain.ps1`
- **Migrate Functions plan:** `functions/migrate-to-consumption.sh`
- **Debug deployment:** `azure/diagnose-api.ps1`
- **Go-live checklist:** `go-live.sh`
- **Verify after deploy:** `verify-live.sh`
