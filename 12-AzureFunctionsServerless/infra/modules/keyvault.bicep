// Part 3: a secret the Function reads via its own managed identity, no key
// or connection string anywhere in app settings. This module creates the
// vault, one demo secret, and the RBAC role assignment that lets the
// Function's identity actually read it -- Managed Identity gives the
// Function an identity; this role assignment is what it's allowed to do
// with it. See EXERCISE.md Part 3.5.
param location string
param keyVaultName string
param tenantId string
param functionAppPrincipalId string
param secretName string
@secure()
param secretValue string
param tags object

// Built-in role: "Key Vault Secrets User" -- get/list secrets, nothing else
// (cannot set, delete, or manage the vault itself). Least privilege for a
// service that only ever reads one value.
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: tenantId
    // RBAC, not vault access policies -- one authorization model
    // (Microsoft.Authorization/roleAssignments) instead of two, and the
    // same model already used for every other resource in this template.
    enableRbacAuthorization: true
    enablePurgeProtection: true
  }
}

resource secret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: secretName
  properties: {
    value: secretValue
  }
}

resource secretsUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, functionAppPrincipalId, keyVaultSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: functionAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output vaultUri string = keyVault.properties.vaultUri
