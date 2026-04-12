// main-consumption.bicep — Azure Durable Functions on Y1 Consumption plan
//
// Replaces main.bicep (Flex Consumption FC1) with the cheaper Y1 Consumption plan.
// Y1 includes 1M free executions/month and 400K GB-s free compute.
// Trade-off: 5-10s cold start on first invocation after idle (acceptable for
// queue-triggered activation pipeline).
//
// Prerequisites (must already exist):
//   - Resource group: real-estate-star-rg
//   - Storage account: realestatestarstore
//   - Application Insights (connection string passed as parameter)
//
// Deploy with:
//   az deployment group create \
//     --resource-group real-estate-star-rg \
//     --template-file main-consumption.bicep \
//     --parameters storageConnectionString="<value>" appInsightsConnectionString="<value>"

@description('Azure region — defaults to the resource group location.')
param location string = resourceGroup().location

@description('Name of the Function App.')
param functionAppName string = 'real-estate-star-functions-v3'

@description('Name of the Consumption hosting plan.')
param planName string = 'real-estate-star-functions-plan-y1'

@description('AzureWebJobsStorage connection string — reuse the existing storage account.')
@secure()
param storageConnectionString string

@description('Application Insights connection string.')
@secure()
param appInsightsConnectionString string

// ---------------------------------------------------------------------------
// Y1 Consumption plan — pay-per-execution, free tier: 1M executions/month
// ---------------------------------------------------------------------------
resource consumptionPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  kind: 'functionapp'
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {
    reserved: true // required for Linux
  }
}

// ---------------------------------------------------------------------------
// Function App — .NET 9 isolated worker, Linux, Y1 Consumption
// ---------------------------------------------------------------------------
resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  properties: {
    serverFarmId: consumptionPlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|9.0'
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: storageConnectionString
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'WEBSITE_USE_PLACEHOLDER_DOTNETISOLATED'
          value: '1'
        }
      ]
      // Function timeout: 10 minutes (max for Consumption plan)
      functionAppScaleLimit: 10
    }
  }
}

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------
output functionAppName string = functionApp.name
output functionAppHostname string = functionApp.properties.defaultHostName
output functionAppId string = functionApp.id
output consumptionPlanId string = consumptionPlan.id
