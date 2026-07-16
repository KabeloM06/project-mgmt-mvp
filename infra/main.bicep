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

@description('Globally unique name for Log Analytics Workspace.')
param logAnalyticsName string = 'project-mgmt-la-${uniqueString(resourceGroup().id)}'

@description('Globally unique name for Application Insights.')
param appInsightsName string = 'project-mgmt-insights-${uniqueString(resourceGroup().id)}'

// ==========================================
// SECURITY IDENTITIES (ENTRA ID / SECURITY FIRST)
// ==========================================

// 1. User-Assigned Managed Identity: Provides passwordless credentials for our applications
resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'proj-mgmt-identity'
  location: location
}

// ==========================================
// OBSERVABILITY & LOGGING LAYER (NEW FOR DAY 10)
// ==========================================

// Log Analytics Workspace (Required for modern Workspace-based Application Insights)
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2021-12-01-preview' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// Application Insights Workspace
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
  }
}

// ==========================================
// COMPUTE LAYER COMPONENT DECLARATIONS
// ==========================================

resource appServicePlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: 'proj-mgmt-free-plan'
  location: location
  sku: {
    name: 'F1'
    tier: 'Free'
  }
}

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

resource appService 'Microsoft.Web/sites@2022-03-01' = {
  name: appServiceName
  location: location
  kind: 'app'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identity.id}': {} // Assigns our Managed Identity to the API
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      netFrameworkVersion: 'v9.0'
      appSettings: [
        {
          name: 'CosmosDb__EndpointUrl'
          value: cosmosDb.properties.documentEndpoint
        }
        {
          name: 'AzureStorage__ConnectionString__queueServiceUri'
          value: 'https://${storage.name}.queue.core.windows.net/'
        }
        {
          name: 'AzureStorage__QueueName'
          value: 'exports'
        }
        // 🔥 Day 10 Add: Linked dynamic connection string directly to avoid configuration override conflicts
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
      ]
    }
  }
}

// RECONCILED FRONTEND APP SERVICE (DAY 7 DRIFT FIX)
resource frontendApp 'Microsoft.Web/sites@2022-03-01' = {
  name: 'project-mgmt-frontend-gyvhzwhlex23g'
  location: location
  kind: 'app'
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      netFrameworkVersion: 'v6.0' 
    }
  }
}

// UPDATED FUNCTION APP WORKER CONFIGURATION
resource functionApp 'Microsoft.Web/sites@2022-03-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identity.id}': {} // 🔥 Assigns our Managed Identity to the Worker
    }
  }
  properties: {
    serverFarmId: functionAppPlan.id
    siteConfig: {
      netFrameworkVersion: 'v9.0'
      appSettings: [
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'CosmosDb__EndpointUrl'
          value: cosmosDb.properties.documentEndpoint
        }
        {
          // Passwordless connection structure using Identity for WebJobs
          name: 'AzureStorage__queueServiceUri'
          value: 'https://${storage.name}.queue.core.windows.net/'
        }
        {
          name: 'AzureStorage__blobServiceUri'
          value: 'https://${storage.name}.blob.core.windows.net/'
        }
        // 🔥 Day 10 Add: App Insights Connection String mapping for the Function runtime
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
      ]
    }
  }
}

// ==========================================
// DATA ACCOUNTS & STORAGE UNDERPINNINGS
// ==========================================

resource storage 'Microsoft.Storage/storageAccounts@2022-09-01' = {
  name: storageName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
  }
}

resource queueService 'Microsoft.Storage/storageAccounts/queueServices@2022-09-01' = {
  parent: storage
  name: 'default'
}

resource exportsQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2022-09-01' = {
  parent: queueService
  name: 'exports'
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2022-09-01' = {
  parent: storage
  name: 'default'
}

resource exportsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-09-01' = {
  parent: blobService
  name: 'exports'
  properties: {
    publicAccess: 'None'
  }
}

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

resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      name: 'standard'
      family: 'A'
    }
    accessPolicies: []
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: false
  }
}

// ==========================================
// RBAC ROLE ASSIGNMENTS (ACCESS CONTROL)
// ==========================================

// Grants our Managed Identity "Storage Blob Data Contributor" access to the Storage Account
resource roleAssignmentBlob 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, 'blobContributor', identity.id)
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// ADDED: Grants our Managed Identity "Storage Queue Data Processor" access to read/write from queues
resource roleAssignmentQueue 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, 'queueProcessor', identity.id)
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '8a0f0c01-4e99-434c-9d83-18e3d0141c7b')
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}
