#!/usr/bin/env bash
set -euo pipefail

###############################################################################
# setup.sh — One-time Azure Container Apps infrastructure setup
#
# Prerequisites:
#   - Azure CLI installed and logged in (`az login`)
#   - Docker image source at apps/api/ (relative to repo root)
#
# This script creates:
#   1. Resource group
#   2. Azure Container Registry (Basic SKU)
#   3. Container Apps Environment
#   4. Builds and pushes the API Docker image via ACR
#   5. Container App with scale-to-zero, secrets, and external ingress
###############################################################################

# --- Configuration -----------------------------------------------------------
RESOURCE_GROUP="real-estate-star-rg"
LOCATION="eastus"
ACR_NAME="realestatestaracr"
ENVIRONMENT="real-estate-star-env"
APP_NAME="real-estate-star-api"
IMAGE_NAME="real-estate-star-api"
IMAGE_TAG="latest"
CONTAINER_PORT=8080
MIN_REPLICAS=0
MAX_REPLICAS=2
CPU="0.25"
MEMORY="0.5Gi"

# Resolve repo root (two levels up from this script)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
API_DIR="$REPO_ROOT/apps/api"

# --- Preflight checks --------------------------------------------------------
echo "==> Checking Azure CLI login..."
az account show --output none 2>/dev/null || {
  echo "ERROR: Not logged in. Run 'az login' first."
  exit 1
}

SUBSCRIPTION=$(az account show --query name --output tsv)
echo "    Using subscription: $SUBSCRIPTION"
echo ""

# --- Step 1: Resource Group --------------------------------------------------
echo "==> Creating resource group '$RESOURCE_GROUP' in '$LOCATION'..."
az group create \
  --name "$RESOURCE_GROUP" \
  --location "$LOCATION" \
  --output none
echo "    Done."
echo ""

# --- Step 2: Azure Container Registry ---------------------------------------
echo "==> Creating Azure Container Registry '$ACR_NAME' (Basic SKU)..."
az acr create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$ACR_NAME" \
  --sku Basic \
  --admin-enabled true \
  --output none
echo "    Done."
echo ""

ACR_LOGIN_SERVER=$(az acr show \
  --name "$ACR_NAME" \
  --query loginServer \
  --output tsv)
echo "    ACR login server: $ACR_LOGIN_SERVER"
echo ""

# --- Step 3: Container Apps Environment --------------------------------------
echo "==> Creating Container Apps Environment '$ENVIRONMENT'..."
az containerapp env create \
  --name "$ENVIRONMENT" \
  --resource-group "$RESOURCE_GROUP" \
  --location "$LOCATION" \
  --output none
echo "    Done."
echo ""

# --- Step 4: Build and push Docker image via ACR ----------------------------
FULL_IMAGE="$ACR_LOGIN_SERVER/$IMAGE_NAME:$IMAGE_TAG"

echo "==> Building and pushing Docker image via ACR..."
echo "    Source: $API_DIR"
echo "    Image:  $FULL_IMAGE"
az acr build \
  --registry "$ACR_NAME" \
  --image "$IMAGE_NAME:$IMAGE_TAG" \
  --file "$API_DIR/Dockerfile" \
  "$API_DIR"
echo "    Done."
echo ""

# --- Step 5: Create Container App --------------------------------------------
echo "==> Creating Container App '$APP_NAME'..."

ACR_PASSWORD=$(az acr credential show \
  --name "$ACR_NAME" \
  --query "passwords[0].value" \
  --output tsv)

az containerapp create \
  --name "$APP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --environment "$ENVIRONMENT" \
  --image "$FULL_IMAGE" \
  --registry-server "$ACR_LOGIN_SERVER" \
  --registry-username "$ACR_NAME" \
  --registry-password "$ACR_PASSWORD" \
  --target-port "$CONTAINER_PORT" \
  --ingress external \
  --min-replicas "$MIN_REPLICAS" \
  --max-replicas "$MAX_REPLICAS" \
  --cpu "$CPU" \
  --memory "$MEMORY" \
  --secrets \
    anthropic-api-key="placeholder" \
    stripe-secret-key="placeholder" \
    stripe-webhook-secret="placeholder" \
    google-client-id="placeholder" \
    google-client-secret="placeholder" \
    cloudflare-api-token="placeholder" \
    cloudflare-account-id="placeholder" \
    scraper-api-key="placeholder" \
    attom-api-key="placeholder" \
  --env-vars \
    Anthropic__ApiKey=secretref:anthropic-api-key \
    Stripe__SecretKey=secretref:stripe-secret-key \
    Stripe__WebhookSecret=secretref:stripe-webhook-secret \
    Google__ClientId=secretref:google-client-id \
    Google__ClientSecret=secretref:google-client-secret \
    Cloudflare__ApiToken=secretref:cloudflare-api-token \
    Cloudflare__AccountId=secretref:cloudflare-account-id \
    ScraperApi__ApiKey=secretref:scraper-api-key \
    Attom__ApiKey=secretref:attom-api-key \
    ASPNETCORE_ENVIRONMENT=Production \
  --output none
echo "    Done."
echo ""

# --- Step 6: Print results ---------------------------------------------------
APP_URL=$(az containerapp show \
  --name "$APP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --query "properties.configuration.ingress.fqdn" \
  --output tsv)

echo "============================================================================"
echo "  Infrastructure setup complete!"
echo "============================================================================"
echo ""
echo "  App URL:  https://$APP_URL"
echo "  Health:   https://$APP_URL/health/live"
echo ""
echo "  Next steps:"
echo "    1. Run ./set-secrets.sh to replace placeholder secrets with real values"
echo "    2. Run ./create-gh-credentials.sh to set up GitHub Actions deployment"
echo "    3. Verify the health endpoint: curl https://$APP_URL/health/live"
echo ""
echo "============================================================================"
