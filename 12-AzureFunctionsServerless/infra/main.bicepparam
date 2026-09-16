using 'main.bicep'

// Replace with something short and unique to you (initials + a few random
// characters) -- it becomes part of the Storage Account and Key Vault
// names, both of which are globally unique across all of Azure.
param nameSuffix = 'CHANGEME'

// Never commit a real secret value here. Pass it instead:
//     az deployment group create ... --parameters main.bicepparam --parameters keyVaultSecretValue='whatever you want'
param keyVaultSecretValue = 'placeholder-replace-at-deploy-time'
