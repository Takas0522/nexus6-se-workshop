@description('Location for the resources')
param location string

@description('Resource tags')
param tags object = {}

@description('AI Search service name')
param searchName string

@description('Partitions for AI Search (number of partitions)')
param partitions int = 1

@description('Replicas for AI Search (number of replicas)')
param replicas int = 1

// AI Search Service
resource aiSearch 'Microsoft.Search/searchServices@2023-11-01' = {
  name: searchName
  location: location
  tags: tags
  sku: {
    name: 'basic'
  }
  properties: {
    partitionCount: partitions
    replicaCount: replicas
    hostingMode: 'default'
    publicNetworkAccess: 'enabled'
  }
  identity: {
    type: 'SystemAssigned'
  }
}

@description('AI Search service endpoint')
output searchEndpoint string = 'https://${searchName}.search.windows.net'

@description('AI Search service name')
output searchName string = aiSearch.name

@description('AI Search service ID')
output searchId string = aiSearch.id

@description('AI Search resource name (FQDN)')
output searchFqdn string = '${searchName}.search.windows.net'
