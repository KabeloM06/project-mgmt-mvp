// ==========================================
// PARAMETERS & PLACEMENT CONFIGURATION
// ==========================================
@description('The targeted Azure geographic region for resource deployments.')
param location string = resourceGroup().location

@description('Globally unique identifier name for the .NET Web API App Service.')
param appServiceName string = 'project-mgmt-api-${uniqueString(resourceGroup().id)}'

@description('Globally unique identifier name for the background Worker Function App.')
param functionAppName string = 'project-mgmt-worker-${uniqueString(resourceGroup().id)}'

@description('Storage Account name specification constraint (lowercase alphanumeric only).')
param storageName string = 'projmgmtstorage${uniqueString(resourceGroup().id)}'

@description('Cosmos DB account identifier string layout.')
param cosmosDbName string = 'project-mgmt-cosmos-${uniqueString(resourceGroup().id)}'

@description('Azure Key Vault instance proxy name allocation.')
param keyVaultName string = 'projmgmtvault-${uniqueString(resourceGroup().id)}'

// ==========================================
// COMPUTE LAYER COMPONENT DECLARATIONS
// ==========================================

// 1. App Service Plan: F1 Free Tier compute for our Frontend API App Service hosting environment
resource appServicePlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: 'proj-mgmt-free-plan'
  location: location
  sku: {
    name: 'F1'
    tier: 'Free'
  }
}

// 2. Function App Plan: Y1 Consumption (Dynamic Serverless) hosting engine for the back-office scheduler
resource functionAppPlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: 'proj-mgmt-consumption-plan'
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
}