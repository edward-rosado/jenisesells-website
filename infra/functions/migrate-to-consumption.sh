#!/usr/bin/env bash
set -euo pipefail

###############################################################################
# migrate-to-consumption.sh — Migrate Functions from Flex Consumption to Y1
#
# This script:
#   1. Deploys a new Y1 Consumption plan + function app (new name to avoid conflict)
#   2. Copies app settings from old function app to new
#   3. Deploys the code to the new function app
#   4. Verifies the new function app is healthy
#   5. Prints instructions for DNS/config cutover and old resource cleanup
#
# Prerequisites:
#   - Azure CLI logged in (`az login`)
#   - Azure Functions Core Tools v4 installed (`npm i -g azure-functions-core-tools@4`)
#   - Run from repo root
#
# This does NOT delete the old function app — that's manual after verification.
###############################################################################

RESOURCE_GROUP="real-estate-star-rg"
OLD_FUNCTION_APP="real-estate-star-functions-v3"
NEW_FUNCTION_APP="real-estate-star-functions-v4"
NEW_PLAN_NAME="real-estate-star-functions-plan-y1"
STORAGE_ACCOUNT="realestatestarstore"

echo "=== Step 1: Get existing app settings ==="
STORAGE_CONN=$(az functionapp config appsettings list \
  --name "$OLD_FUNCTION_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --query "[?name=='AzureWebJobsStorage'].value" -o tsv)

APPINSIGHTS_CONN=$(az functionapp config appsettings list \
  --name "$OLD_FUNCTION_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --query "[?name=='APPLICATIONINSIGHTS_CONNECTION_STRING'].value" -o tsv)

echo "Storage connection: ${STORAGE_CONN:0:30}..."
echo "App Insights: ${APPINSIGHTS_CONN:0:30}..."

echo ""
echo "=== Step 2: Deploy Y1 Consumption plan via Bicep ==="
az deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --template-file infra/functions/main-consumption.bicep \
  --parameters \
    functionAppName="$NEW_FUNCTION_APP" \
    planName="$NEW_PLAN_NAME" \
    storageConnectionString="$STORAGE_CONN" \
    appInsightsConnectionString="$APPINSIGHTS_CONN"

echo ""
echo "=== Step 3: Copy additional app settings ==="
# Get all custom app settings from old function app (exclude built-in ones)
SETTINGS=$(az functionapp config appsettings list \
  --name "$OLD_FUNCTION_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --query "[?name!='AzureWebJobsStorage' && name!='APPLICATIONINSIGHTS_CONNECTION_STRING' && name!='FUNCTIONS_EXTENSION_VERSION' && name!='FUNCTIONS_WORKER_RUNTIME' && name!='WEBSITE_USE_PLACEHOLDER_DOTNETISOLATED' && !starts_with(name, 'WEBSITE_')]" -o json)

# Apply settings to new function app
echo "$SETTINGS" | jq -r '.[] | "\(.name)=\(.value)"' | while read -r setting; do
  az functionapp config appsettings set \
    --name "$NEW_FUNCTION_APP" \
    --resource-group "$RESOURCE_GROUP" \
    --settings "$setting" --output none
done
echo "Custom app settings copied."

echo ""
echo "=== Step 4: Build and deploy code ==="
cd apps/api/RealEstateStar.Functions

dotnet build --configuration Release --framework net9.0
dotnet publish --configuration Release --framework net9.0 --no-build --output ./bin/output

OBJ_DIR=obj/Release/net9.0
cp "$OBJ_DIR/functions.metadata" ./bin/output/
cp "$OBJ_DIR/worker.config.json" ./bin/output/

EXTENSIONS_DIR=$(find obj -path "*/WorkerExtensions/bin/Release/net8.0" -type d | head -1)
cp -r "$EXTENSIONS_DIR/." ./bin/output/.azurefunctions/
DEPS_FILE=$(find ./bin/output/.azurefunctions -name "function.deps.json" | head -1)
[ -n "$DEPS_FILE" ] && cp "$DEPS_FILE" ./bin/output/.azurefunctions/function.deps.json 2>/dev/null || true

cp -r ../../../config ./bin/output/config
echo '{"IsEncrypted":false,"Values":{"FUNCTIONS_WORKER_RUNTIME":"dotnet-isolated"}}' > ./bin/output/local.settings.json

cd bin/output
func azure functionapp publish "$NEW_FUNCTION_APP" --no-build --dotnet-isolated

cd ../../../../../..

echo ""
echo "=== Step 5: Verify deployment ==="
sleep 30
echo "Checking function discovery..."
FUNCS=$(az rest --method get \
  --url "https://management.azure.com/subscriptions/$(az account show --query id -o tsv)/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Web/sites/$NEW_FUNCTION_APP/functions?api-version=2023-12-01" \
  --query "value | length(@)" --output tsv 2>/dev/null || echo "0")
echo "Functions discovered: $FUNCS"

HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "https://$NEW_FUNCTION_APP.azurewebsites.net/api/health" || echo "000")
echo "Health endpoint: HTTP $HTTP_CODE"

echo ""
echo "=== Migration Summary ==="
echo "Old function app: $OLD_FUNCTION_APP (Flex Consumption — keep running until cutover)"
echo "New function app: $NEW_FUNCTION_APP (Y1 Consumption — verify, then cut over)"
echo ""
echo "=== Manual steps after verification ==="
echo "1. Update deploy-api.yml: AzureFunctions__HealthUrl → https://$NEW_FUNCTION_APP.azurewebsites.net/api/health"
echo "2. Update deploy-functions.yml: FUNCTION_APP_NAME → $NEW_FUNCTION_APP"
echo "3. Update queue triggers: verify activation-requests queue points to $NEW_FUNCTION_APP"
echo "4. Test activation pipeline end-to-end on the new function app"
echo "5. After verification, delete old resources:"
echo "   az functionapp delete --name $OLD_FUNCTION_APP --resource-group $RESOURCE_GROUP"
echo "   az appservice plan delete --name real-estate-star-functions-plan --resource-group $RESOURCE_GROUP"
