@description('Location for the resources')
param location string

@description('Resource tags')
param tags object = {}

@description('Container Registry name')
param registryName string

// Container Registry
resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: registryName
  location: location
  tags: tags
  sku: {
    name: 'Premium'
  }
  properties: {
    adminUserEnabled: false
    publicNetworkAccess: 'Enabled'
    networkRuleBypassOptions: 'AzureServices'
    policies: {
      quarantinePolicy: {
        status: 'disabled'
      }
      trustPolicy: {
        type: 'Notary'
        status: 'disabled'
      }
      retentionPolicy: {
        days: 30
        status: 'enabled'
      }
    }
  }
}

@description('Container Registry login server')
output acrLoginServer string = containerRegistry.properties.loginServer

@description('Container Registry name')
output acrName string = containerRegistry.name

@description('Container Registry resource ID')
output acrId string = containerRegistry.id
