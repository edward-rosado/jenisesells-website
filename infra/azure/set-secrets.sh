#!/usr/bin/env bash
set -euo pipefail

###############################################################################
# set-secrets.sh — Set real secret values on the Azure Container App
#
# Prerequisites:
#   - Azure CLI installed and logged in (`az login`)
#   - Container App already created via setup.sh
#
# This script prompts for each secret value interactively and updates the
# Container App secrets. No secrets are hardcoded or logged.
###############################################################################

# --- Configuration -----------------------------------------------------------
RESOURCE_GROUP="real-estate-star-rg"
APP_NAME="real-estate-star-api"

# Secrets to configure (name shown to user : Azure secret name)
declare -a SECRETS=(
  "anthropic-api-key|Anthropic API Key"
  "stripe-secret-key|Stripe Secret Key"
  "stripe-webhook-secret|Stripe Webhook Secret"
  "google-client-id|Google Client ID"
  "google-client-secret|Google Client Secret"
  "cloudflare-api-token|Cloudflare API Token"
  "cloudflare-account-id|Cloudflare Account ID"
  "scraper-api-key|ScraperAPI Key"
  "attom-api-key|ATTOM API Key"
)

# --- Preflight checks --------------------------------------------------------
echo "==> Checking Azure CLI login..."
az account show --output none 2>/dev/null || {
  echo "ERROR: Not logged in. Run 'az login' first."
  exit 1
}

echo "==> Verifying Container App '$APP_NAME' exists..."
az containerapp show \
  --name "$APP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --output none 2>/dev/null || {
  echo "ERROR: Container App '$APP_NAME' not found in resource group '$RESOURCE_GROUP'."
  echo "       Run setup.sh first to create the infrastructure."
  exit 1
}
echo ""

# --- Collect secrets ---------------------------------------------------------
echo "============================================================================"
echo "  Enter secret values for $APP_NAME"
echo "  (input is hidden — paste carefully)"
echo "============================================================================"
echo ""

declare -a SECRET_ARGS=()
SKIPPED=0

for entry in "${SECRETS[@]}"; do
  SECRET_NAME="${entry%%|*}"
  DISPLAY_NAME="${entry##*|}"

  echo -n "  $DISPLAY_NAME ($SECRET_NAME): "
  read -rs SECRET_VALUE
  echo ""

  if [[ -z "$SECRET_VALUE" ]]; then
    echo "    -> Skipped (empty value). Secret will keep its current value."
    ((SKIPPED++))
    continue
  fi

  SECRET_ARGS+=("${SECRET_NAME}=${SECRET_VALUE}")
done

echo ""

if [[ ${#SECRET_ARGS[@]} -eq 0 ]]; then
  echo "No secrets to update. Exiting."
  exit 0
fi

# --- Apply secrets -----------------------------------------------------------
echo "==> Updating ${#SECRET_ARGS[@]} secret(s) on '$APP_NAME'..."

az containerapp secret set \
  --name "$APP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --secrets "${SECRET_ARGS[@]}" \
  --output none

echo "    Done."
echo ""

# --- Summary -----------------------------------------------------------------
echo "============================================================================"
echo "  Secrets updated: ${#SECRET_ARGS[@]}"
echo "  Secrets skipped: $SKIPPED"
echo "============================================================================"
echo ""
echo "  The Container App will pick up new secret values on next revision."
echo "  To force a restart now, run:"
echo ""
echo "    az containerapp revision restart \\"
echo "      --name $APP_NAME \\"
echo "      --resource-group $RESOURCE_GROUP \\"
echo "      --revision \$(az containerapp revision list \\"
echo "        --name $APP_NAME \\"
echo "        --resource-group $RESOURCE_GROUP \\"
echo "        --query '[0].name' --output tsv)"
echo ""
echo "============================================================================"
