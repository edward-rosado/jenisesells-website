#!/usr/bin/env bash
set -euo pipefail

###############################################################################
# deploy.sh — Provision Azure Durable Functions infrastructure
#
# Deploys main.bicep into the existing real-estate-star-rg resource group.
# The Function App starts EMPTY — no code is deployed here.
#
# Prerequisites:
#   - Azure CLI installed and logged in (az login)
#   - Correct subscription set (az account set --subscription <id>)
#   - AZURE_STORAGE_CONN_STR env var set (or pass --storage-conn-str flag)
#   - APP_INSIGHTS_CONN_STR env var set (or pass --app-insights-conn-str flag)
#
# Usage:
#   ./deploy.sh
#   ./deploy.sh --function-app-name my-custom-name
#   AZURE_STORAGE_CONN_STR="..." APP_INSIGHTS_CONN_STR="..." ./deploy.sh
###############################################################################

RESOURCE_GROUP="real-estate-star-rg"
FUNCTION_APP_NAME="real-estate-star-functions"
PLAN_NAME="real-estate-star-functions-plan"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BICEP_FILE="$SCRIPT_DIR/main.bicep"

# --- Parse flags -------------------------------------------------------------
while [[ $# -gt 0 ]]; do
  case "$1" in
    --function-app-name)
      FUNCTION_APP_NAME="$2"; shift 2 ;;
    --plan-name)
      PLAN_NAME="$2"; shift 2 ;;
    --resource-group)
      RESOURCE_GROUP="$2"; shift 2 ;;
    --storage-conn-str)
      AZURE_STORAGE_CONN_STR="$2"; shift 2 ;;
    --app-insights-conn-str)
      APP_INSIGHTS_CONN_STR="$2"; shift 2 ;;
    *)
      echo "Unknown flag: $1" >&2; exit 1 ;;
  esac
done

# --- Validate required secrets -----------------------------------------------
STORAGE_CONN_STR="${AZURE_STORAGE_CONN_STR:-}"
APP_INSIGHTS_CONN="${APP_INSIGHTS_CONN_STR:-}"

if [[ -z "$STORAGE_CONN_STR" ]]; then
  echo "ERROR: AZURE_STORAGE_CONN_STR is not set."
  echo "       Export it or pass --storage-conn-str."
  echo "       (Same value as AzureStorage__ConnectionString on the Container App.)"
  exit 1
fi

if [[ -z "$APP_INSIGHTS_CONN" ]]; then
  echo "ERROR: APP_INSIGHTS_CONN_STR is not set."
  echo "       Export it or pass --app-insights-conn-str."
  echo "       (Retrieve from: az monitor app-insights component show --app <name> -g $RESOURCE_GROUP --query connectionString -o tsv)"
  exit 1
fi

# --- Preflight checks --------------------------------------------------------
echo "==> Checking prerequisites..."

if ! command -v az &>/dev/null; then
  echo "ERROR: Azure CLI not found. Install: https://aka.ms/installazurecliwindows"
  exit 1
fi

if ! az account show --output none 2>/dev/null; then
  echo "ERROR: Not logged in to Azure. Run: az login"
  exit 1
fi

SUBSCRIPTION=$(az account show --query name --output tsv)
echo "    Subscription : $SUBSCRIPTION"
echo "    Resource group: $RESOURCE_GROUP"
echo "    Function App  : $FUNCTION_APP_NAME"
echo ""

# Verify the resource group exists (don't create it — it must match the Container App's RG)
if ! az group exists --name "$RESOURCE_GROUP" | grep -q true; then
  echo "ERROR: Resource group '$RESOURCE_GROUP' does not exist."
  echo "       The Function App must be in the same RG as the Container App."
  exit 1
fi

# --- Deploy Bicep template ---------------------------------------------------
DEPLOYMENT_NAME="functions-$(date +%Y%m%d-%H%M%S)"

echo "==> Deploying Bicep template..."
echo "    Deployment name: $DEPLOYMENT_NAME"
echo "    Template: $BICEP_FILE"
echo ""

az deployment group create \
  --name "$DEPLOYMENT_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --template-file "$BICEP_FILE" \
  --parameters \
    functionAppName="$FUNCTION_APP_NAME" \
    planName="$PLAN_NAME" \
    storageConnectionString="$STORAGE_CONN_STR" \
    appInsightsConnectionString="$APP_INSIGHTS_CONN" \
  --output table

echo ""

# --- Read outputs ------------------------------------------------------------
HOSTNAME=$(az deployment group show \
  --name "$DEPLOYMENT_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --query "properties.outputs.functionAppHostname.value" \
  --output tsv 2>/dev/null || echo "")

echo "============================================================================"
echo "  Deployment complete!"
echo "============================================================================"
echo ""
if [[ -n "$HOSTNAME" ]]; then
  echo "  Function App URL : https://${HOSTNAME}"
  echo "  Health endpoint  : https://${HOSTNAME}/api/health  (once functions are deployed)"
fi
echo ""
echo "  Next steps:"
echo "    1. Add AZURE_FUNCTIONS_APP_NAME='$FUNCTION_APP_NAME' to GitHub Actions secrets"
echo "    2. Run the deploy-functions GitHub Actions job to publish function code"
echo "    3. Verify: az functionapp show --name $FUNCTION_APP_NAME --resource-group $RESOURCE_GROUP"
echo ""
echo "============================================================================"
