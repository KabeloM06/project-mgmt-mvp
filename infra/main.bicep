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
// SECURITY IDENTITIES (ENTRA ID / SECURITY FIRST)
// ==========================================

// 1. User-Assigned Managed Identity: Provides passwordless credentials for our applications
resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'proj-mgmt-identity'
  location: location
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
    }
  }
}

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

// 2. Role Assignment: Grants our Managed Identity "Storage Blob Data Contributor" access to the Storage Account
resource roleAssignmentBlob 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, 'blobContributor', identity.id)
  scope: storage
  properties: {
    // Built-in Azure GUID for Storage Blob Data Contributor role
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}
