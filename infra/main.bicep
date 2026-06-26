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

// ==========================================
// HOSTING RUNTIMES & WEB APP TARGETS
// ==========================================

// 3. API Web App: Hosts our Core .NET 9 API proxy layer
resource appService 'Microsoft.Web/sites@2022-03-01' = {
  name: appServiceName
  location: location
  kind: 'app'
  properties: {
    serverFarmId: appServicePlan.id // Links directly to the compute plan from Commit 1
    siteConfig: {
      netFrameworkVersion: 'v9.0' // Explicitly sets the engine to .NET 9
    }
  }
}

// 4. Background Function App: Hosts the serverless queue handlers
resource functionApp 'Microsoft.Web/sites@2022-03-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp'
  properties: {
    serverFarmId: functionAppPlan.id // Links to the Consumption plan from Commit 1
  }
}

// ==========================================
// DATA ACCOUNTS & STORAGE UNDERPINNINGS
// ==========================================

// 5. Storage Account: Handles the background system blobs, queues, and metadata
resource storage 'Microsoft.Storage/storageAccounts@2022-09-01' = {
  name: storageName
  location: location
  sku: {
    name: 'Standard_LRS' // Lowest cost, locally redundant replication tier
  }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
  }
}

// ==========================================
// PERSISTENCE & SECURITY VAULT TIERS
// ==========================================

// 6. Cosmos DB Account: Direct NoSQL data persistence layer
resource cosmosDb 'Microsoft.DocumentDB/databaseAccounts@2023-04-15' = {
  name: cosmosDbName
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
  }
}

// 7. Azure Key Vault: Centralized secret management repository
resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      name: 'standard'
      family: 'A'
    }
    accessPolicies: [] // Left blank intentionally; we will configure secure access shortly
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: false
  }
}
