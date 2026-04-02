# Azure Durable Functions Infrastructure

Provisions the Azure Durable Functions hosting environment that will run the
orchestrated lead, CMA, and home-search pipelines as part of the DF migration.

## Resources created

| Resource | Name | Notes |
|----------|------|-------|
| Flex Consumption plan | `real-estate-star-functions-plan` | FC1 SKU, Linux |
| Function App | `real-estate-star-functions` | .NET 10 isolated worker |
| Blob container | `function-packages` | In existing storage account; used by Flex Consumption for code deployment |

## What is NOT created here

These resources already exist and are reused:

| Resource | Name | Source |
|----------|------|--------|
| Resource group | `real-estate-star-rg` | Created by `infra/azure/setup.sh` |
| Storage account | `realestarestarsa` | Created by `infra/azure/provision-keyvault.ps1` |
| Application Insights | Shared with Container App | Pass connection string as parameter |

## Prerequisites

- Azure CLI installed and logged in (`az login`)
- Correct subscription active (`az account set --subscription <id>`)
- `real-estate-star-rg` resource group exists (it does in production)
- Two secrets ready (see below)

## Configuration values needed

| Value | Where to find it |
|-------|-----------------|
| `AZURE_STORAGE_CONN_STR` | Same value as `AzureStorage__ConnectionString` on the Container App; retrieve with: `az containerapp secret show --name real-estate-star-api -g real-estate-star-rg` |
| `APP_INSIGHTS_CONN_STR` | `az monitor app-insights component show --app <name> -g real-estate-star-rg --query connectionString -o tsv` — or find in Azure Portal under the existing App Insights resource |

## Deploy

```bash
export AZURE_STORAGE_CONN_STR="DefaultEndpointsProtocol=https;AccountName=realestarestarsa;..."
export APP_INSIGHTS_CONN_STR="InstrumentationKey=...;IngestionEndpoint=..."

cd infra/functions
chmod +x deploy.sh
./deploy.sh
```

The script validates prerequisites, checks the resource group exists, deploys
`main.bicep`, and prints the Function App URL on success.

## Verify deployment

```bash
# Check the Function App was created
az functionapp show \
  --name real-estate-star-functions \
  --resource-group real-estate-star-rg \
  --query "{name:name, state:state, defaultHostName:defaultHostName}" \
  -o table

# List app settings (confirm runtime config)
az functionapp config appsettings list \
  --name real-estate-star-functions \
  --resource-group real-estate-star-rg \
  -o table
```

The Function App starts **empty** — no orchestrators or activities are deployed
yet. Function code is published in Phases 1-3 of the DF migration via the CI
pipeline (see `.github/workflows/deploy-api.yml` for the Container App pattern
to follow).

## Relationship to existing Container App infra

```
real-estate-star-rg
├── real-estate-star-api           (Container App — HTTP layer, stays as-is)
├── real-estate-star-env           (Container Apps Environment)
├── realestarestarsa               (Storage Account — shared by both)
│   └── function-packages/         (new: Flex Consumption deployment container)
├── real-estate-star-kv            (Key Vault — Data Protection keys)
└── real-estate-star-functions     (NEW: Durable Functions app)
    └── real-estate-star-functions-plan  (NEW: Flex Consumption plan)
```

The Container App continues to handle all HTTP traffic. When a lead is
submitted, the API enqueues a storage queue message. The Function App (once
functions are deployed) picks up the message and runs the durable orchestrator.

## always-ready instance

The Bicep template configures `alwaysReady: [{ name: 'http', instanceCount: 1 }]`
for the HTTP trigger group. This keeps one warm instance to avoid cold-start
latency on the first lead of the day. At FC1 pricing this costs roughly
$0.06/day — negligible for production reliability.

To disable (e.g., in staging), remove the `alwaysReady` block from
`main.bicep` and redeploy.
