#!/usr/bin/env bash
set -euo pipefail

###############################################################################
# create-gh-credentials.sh — Create Azure service principal for GitHub Actions
#
# Prerequisites:
#   - Azure CLI installed and logged in (`az login`)
#   - Resource group and ACR already created via setup.sh
#
# This script:
#   1. Creates a service principal with Contributor role on the resource group
#   2. Assigns AcrPush role on the container registry
#   3. Outputs the JSON credentials for the AZURE_CREDENTIALS GitHub secret
###############################################################################

# --- Configuration -----------------------------------------------------------
RESOURCE_GROUP="real-estate-star-rg"
ACR_NAME="realestatestaracr"
SP_NAME="github-actions-real-estate-star"

# --- Preflight checks --------------------------------------------------------
echo "==> Checking Azure CLI login..."
az account show --output none 2>/dev/null || {
  echo "ERROR: Not logged in. Run 'az login' first."
  exit 1
}

SUBSCRIPTION_ID=$(az account show --query id --output tsv)
echo "    Subscription ID: $SUBSCRIPTION_ID"
echo ""

echo "==> Verifying resource group '$RESOURCE_GROUP' exists..."
az group show --name "$RESOURCE_GROUP" --output none 2>/dev/null || {
  echo "ERROR: Resource group '$RESOURCE_GROUP' not found."
  echo "       Run setup.sh first to create the infrastructure."
  exit 1
}

echo "==> Verifying ACR '$ACR_NAME' exists..."
az acr show --name "$ACR_NAME" --output none 2>/dev/null || {
  echo "ERROR: Container registry '$ACR_NAME' not found."
  echo "       Run setup.sh first to create the infrastructure."
  exit 1
}
echo ""

# --- Step 1: Create service principal with Contributor on resource group ------
RG_SCOPE="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP"

echo "==> Creating service principal '$SP_NAME'..."
echo "    Scope: $RG_SCOPE"
echo "    Role:  Contributor"

SP_OUTPUT=$(az ad sp create-for-rbac \
  --name "$SP_NAME" \
  --role Contributor \
  --scopes "$RG_SCOPE" \
  --sdk-auth \
  2>/dev/null)

echo "    Done."
echo ""

# --- Step 2: Assign AcrPush role on the container registry -------------------
ACR_ID=$(az acr show \
  --name "$ACR_NAME" \
  --query id \
  --output tsv)

SP_APP_ID=$(echo "$SP_OUTPUT" | python3 -c "import sys,json; print(json.load(sys.stdin)['clientId'])" 2>/dev/null || \
            echo "$SP_OUTPUT" | jq -r '.clientId' 2>/dev/null)

echo "==> Assigning AcrPush role on '$ACR_NAME'..."
echo "    Service principal: $SP_APP_ID"

az role assignment create \
  --assignee "$SP_APP_ID" \
  --role AcrPush \
  --scope "$ACR_ID" \
  --output none

echo "    Done."
echo ""

# --- Output credentials ------------------------------------------------------
echo "============================================================================"
echo "  Service principal created successfully!"
echo "============================================================================"
echo ""
echo "  Copy the JSON below and save it as a GitHub repository secret"
echo "  named AZURE_CREDENTIALS:"
echo ""
echo "    1. Go to: https://github.com/<owner>/<repo>/settings/secrets/actions"
echo "    2. Click 'New repository secret'"
echo "    3. Name:  AZURE_CREDENTIALS"
echo "    4. Value: (paste the JSON below)"
echo ""
echo "--- BEGIN AZURE_CREDENTIALS ---"
echo "$SP_OUTPUT"
echo "--- END AZURE_CREDENTIALS ---"
echo ""
echo "  Additional GitHub secrets to set:"
echo "    AZURE_RG_NAME         = $RESOURCE_GROUP"
echo "    AZURE_ACR_NAME        = $ACR_NAME"
echo "    AZURE_APP_NAME        = real-estate-star-api"
echo ""
echo "============================================================================"
