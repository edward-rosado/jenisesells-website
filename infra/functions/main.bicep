// main.bicep — Azure Durable Functions infrastructure for Real Estate Star
//
// Provisions a Flex Consumption Function App that will host the durable
// orchestrators introduced in the DF migration.  The Function App starts
// EMPTY — no code is deployed here; that happens through the CI pipeline.
//
// Prerequisites (must already exist):
//   - Resource group: real-estate-star-rg
//   - Storage account: realestatestarstore (same one Container App uses)
//   - Application Insights: reused via appInsightsConnectionString param
//
// Deploy with:
//   az deployment group create \
//     --resource-group real-estate-star-rg \
//     --template-file main.bicep \
//     --parameters storageConnectionString="<value>" appInsightsConnectionString="<value>"

@description('Azure region — defaults to the resource group location.')
param location string = resourceGroup().location

@description('Name of the Function App.')
param functionAppName string = 'real-estate-star-functions'

@description('Name of the Flex Consumption hosting plan.')
param planName string = 'real-estate-star-functions-plan'

@description('AzureWebJobsStorage connection string — reuse the existing storage account.')
@secure()
param storageConnectionString string

@description('Application Insights connection string — reuse the existing Container App\'s App Insights instance.')
@secure()
param appInsightsConnectionString string

// ---------------------------------------------------------------------------
// Flex Consumption plan
// FC1 = Flex Consumption SKU. No pre-warmed instances are billed while idle;
// always-ready instances are billed only when declared (pennies/month for 1).
// ---------------------------------------------------------------------------
resource flexPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  kind: 'functionapp'
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  properties: {
    reserved: true // required for Linux
  }
}

// ---------------------------------------------------------------------------
// Function App — .NET 10 isolated worker, Linux, Flex Consumption
// ---------------------------------------------------------------------------
resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  properties: {
    serverFarmId: flexPlan.id
    siteConfig: {
      // Flex Consumption uses a different config surface; runtime settings go
      // under functionAppConfig below, NOT under siteConfig.linuxFxVersion.
    }
    functionAppConfig: {
      runtime: {
        name: 'dotnet-isolated'
        version: '10.0'
      }
      scaleAndConcurrency: {
        // One always-ready instance for the lead orchestrator to avoid cold
        // starts on first lead submission of the day. Cost: ~$0.06/day.
        alwaysReady: [
          {
            name: 'http'
            instanceCount: 1
          }
        ]
        maximumInstanceCount: 10
        instanceMemoryMB: 2048
      }
      deployment: {
        // Storage used for internal Flex Consumption package deployment.
        storage: {
          type: 'blobContainer'
          value: '${storageAccount.properties.primaryEndpoints.blob}function-packages'
          authentication: {
            type: 'StorageAccountConnectionString'
            storageAccountConnectionStringName: 'AzureWebJobsStorage'
          }
        }
      }
    }
    httpsOnly: true
  }

  // App settings — mix of runtime requirements and secrets.
  // Secrets are passed as secure parameters; never hardcoded.
  resource appSettings 'config' = {
    name: 'appsettings'
    properties: {
      // --- Storage ---
      AzureWebJobsStorage: storageConnectionString

      // --- Observability ---
      APPLICATIONINSIGHTS_CONNECTION_STRING: appInsightsConnectionString

      // --- .NET 10 isolated worker ---
      // Required for the out-of-process model on Flex Consumption.
      WEBSITE_USE_PLACEHOLDER_DOTNETISOLATED: '1'

      // NOTE: FUNCTIONS_WORKER_RUNTIME and FUNCTIONS_EXTENSION_VERSION are NOT
      // allowed as app settings on Flex Consumption. They are configured via
      // functionAppConfig.runtime above.
    }
  }
}

// ---------------------------------------------------------------------------
// Blob container for Flex Consumption package deployment
// The storage account already exists; we just add the container.
// ---------------------------------------------------------------------------
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: 'realestatestarstore'
}

resource functionPackageContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  name: '${storageAccount.name}/default/function-packages'
  properties: {
    publicAccess: 'None'
  }
}

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------
output functionAppName string = functionApp.name
output functionAppHostname string = functionApp.properties.defaultHostName
output functionAppId string = functionApp.id
output flexPlanId string = flexPlan.id
