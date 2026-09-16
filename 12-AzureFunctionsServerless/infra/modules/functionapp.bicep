param location string
param functionAppName string
param storageAccountName string
param appInsightsConnectionString string
param tags object

// Non-secret configuration only -- see EXERCISE.md Part 3 for why none of
// this needs to be a Key Vault reference: a tenant ID, an audience, a role
// name and a vault URI are not secrets. The one thing that IS sensitive
// (the demo secret's value) never appears here; the Function fetches it at
// runtime via its managed identity.
param entraIdTenantId string
param entraIdAudience string
param entraIdRequiredAppRole string
param keyVaultUri string
param keyVaultSecretName string

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

// Consumption plan (Y1): true pay-per-execution serverless pricing, the
// cheapest hosting option for this exercise, at the cost of cold starts.
// Swap to EP1 (Premium) if cold starts become the thing you're teaching
// instead of auth.
resource hostingPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${functionAppName}-plan'
  location: location
  tags: tags
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
}

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  tags: tags
  kind: 'functionapp'
  identity: {
    // System-assigned managed identity -- see keyvault.bicep for the RBAC
    // role assignment that lets this identity read the demo secret.
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: hostingPlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v9.0'
      use32BitWorkerProcess: false
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        { name: 'AzureWebJobsStorage', value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net' }
        { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
        { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
        { name: 'WEBSITE_RUN_FROM_PACKAGE', value: '1' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
        { name: 'EntraId__TenantId', value: entraIdTenantId }
        { name: 'EntraId__Audience', value: entraIdAudience }
        { name: 'EntraId__RequiredAppRole', value: entraIdRequiredAppRole }
        { name: 'KeyVault__VaultUri', value: keyVaultUri }
        { name: 'KeyVault__SecretName', value: keyVaultSecretName }
      ]
    }
  }
}

output functionAppName string = functionApp.name
output defaultHostName string = functionApp.properties.defaultHostName
output principalId string = functionApp.identity.principalId
