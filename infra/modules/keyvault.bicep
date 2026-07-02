@description('Location for the resources')
param location string

@description('Resource tags')
param tags object = {}

@description('Key Vault name')
param vaultName string

// Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: vaultName
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    accessPolicies: []
    enableSoftDelete: true
    enablePurgeProtection: true
    enableRbacAuthorization: true
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

@description('Key Vault URI')
output vaultUri string = keyVault.properties.vaultUri

@description('Key Vault name')
output vaultName string = keyVault.name

@description('Key Vault resource ID')
output vaultId string = keyVault.id
