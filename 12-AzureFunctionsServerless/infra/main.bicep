// Deploys module 12's Function API. Run from the CLI, not this repo's CI:
//     az group create --name <rg-name> --location <location>
//     az deployment group create --resource-group <rg-name> --template-file main.bicep --parameters main.bicepparam
// See GETTING_STARTED.md Part 4 for the full walkthrough, cost notes, and
// the one manual prerequisite this template cannot automate (the Entra ID
// directory role grant appRegistrations.bicep's comment explains).
targetScope = 'resourceGroup'

@description('Azure region for every resource in this deployment.')
param location string = resourceGroup().location

@description('Short, unique suffix appended to resource names (e.g. your initials + a random string) to avoid global-name collisions on the Storage Account and Key Vault.')
@minLength(3)
@maxLength(10)
param nameSuffix string

@description('Display name for the API app registration.')
param apiAppDisplayName string = 'netlearn-functions-api'

@description('Display name for the client app registration used to request tokens.')
param clientAppDisplayName string = 'netlearn-functions-client'

@description('App role value the client must be assigned before it can call the API.')
param appRoleValue string = 'Notes.Access'

@description('Name of the demo secret stored in Key Vault.')
param keyVaultSecretName string = 'demo-secret'

@description('Value of the demo secret. Pass this at deploy time (main.bicepparam or --parameters), never commit a real value.')
@secure()
param keyVaultSecretValue string

var tags = {
  module: '12-AzureFunctionsServerless'
  managedBy: 'bicep'
}

var keyVaultName = 'kv-${nameSuffix}-func'
// Deterministic from the name, not a module output -- see the note on
// keyVault/functionApp ordering below for why that matters here.
// environment().suffixes.keyvaultDns (not a hardcoded ".vault.azure.net")
// so this template still works unmodified in Azure Government/China clouds.
var keyVaultUri = 'https://${keyVaultName}${environment().suffixes.keyvaultDns}'

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    location: location
    storageAccountName: 'st${nameSuffix}func'
    tags: tags
  }
}

module appInsights 'modules/appinsights.bicep' = {
  name: 'appInsights'
  params: {
    location: location
    appInsightsName: 'appi-${nameSuffix}-func'
    logAnalyticsWorkspaceName: 'log-${nameSuffix}-func'
    tags: tags
  }
}

module appRegistrations 'modules/appRegistrations.bicep' = {
  name: 'appRegistrations'
  params: {
    location: location
    uamiName: 'id-${nameSuffix}-appreg'
    apiAppDisplayName: apiAppDisplayName
    clientAppDisplayName: clientAppDisplayName
    appRoleValue: appRoleValue
  }
}

// functionApp needs the vault's URI; keyVault needs the Function's managed
// identity (created BY functionApp) to grant it access. Passing each other's
// live module output would be a circular dependency, which Bicep rejects
// outright -- so keyVaultUri is built from the (deterministic) name above
// instead of read from the keyVault module's output, breaking the cycle:
// functionApp no longer depends on keyVault at all, only the other way
// around. See EXERCISE.md Part 3.6.
module functionApp 'modules/functionapp.bicep' = {
  name: 'functionApp'
  params: {
    location: location
    functionAppName: 'func-${nameSuffix}-notes'
    storageAccountName: storage.outputs.storageAccountName
    appInsightsConnectionString: appInsights.outputs.connectionString
    entraIdTenantId: tenant().tenantId
    entraIdAudience: 'api://${appRegistrations.outputs.apiAppId}'
    entraIdRequiredAppRole: appRoleValue
    keyVaultUri: keyVaultUri
    keyVaultSecretName: keyVaultSecretName
    tags: tags
  }
}

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyVault'
  params: {
    location: location
    keyVaultName: keyVaultName
    tenantId: tenant().tenantId
    functionAppPrincipalId: functionApp.outputs.principalId
    secretName: keyVaultSecretName
    secretValue: keyVaultSecretValue
    tags: tags
  }
}

output functionAppName string = functionApp.outputs.functionAppName
output functionAppHostName string = functionApp.outputs.defaultHostName
output apiAppId string = appRegistrations.outputs.apiAppId
output clientAppId string = appRegistrations.outputs.clientAppId
output tenantId string = tenant().tenantId
@secure()
output clientSecret string = appRegistrations.outputs.clientSecret
