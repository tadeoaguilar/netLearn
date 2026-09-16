// Azure Functions requires a Storage Account for its own runtime bookkeeping
// (host lease coordination, trigger state) even for an HTTP-only function
// with no application data -- see EXERCISE.md Part 1.3. This is that
// account, and nothing else uses it.
param location string
param storageAccountName string
param tags object

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
  }
}

output storageAccountName string = storageAccount.name
output storageAccountId string = storageAccount.id
