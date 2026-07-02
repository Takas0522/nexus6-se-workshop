@description('Location for the resources')
param location string

@description('Resource tags')
param tags object = {}

@description('Skills Storage Account name')
param skillsStorageName string

@description('Portal Storage Account name')
param portalStorageName string

// Skills Storage Account (V2, Standard_LRS)
resource skillsStorageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: skillsStorageName
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    allowSharedKeyAccess: true
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
    }
  }
}

// Portal Storage Account (V2, Standard_LRS) with static website enabled
resource portalStorageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: portalStorageName
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    allowSharedKeyAccess: true
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
    }
  }
}

// Enable static website on portal storage account
resource portalStaticWebsite 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  name: '${portalStorageAccount.name}/default/$web'
}

@description('Skills Storage Account name')
output skillsStorageName string = skillsStorageAccount.name

@description('Skills Storage Account ID')
output skillsStorageId string = skillsStorageAccount.id

@description('Portal Storage Account name')
output portalStorageName string = portalStorageAccount.name

@description('Portal Storage Account ID')
output portalStorageId string = portalStorageAccount.id

@description('Portal static web endpoint')
output portalStaticWebEndpoint string = portalStorageAccount.properties.primaryEndpoints.web

@description('Skills Storage Account connection string')
#disable-next-line outputs-should-not-contain-secrets
output skillsStorageConnectionString string = 'DefaultEndpointsProtocol=https;AccountName=${skillsStorageAccount.name};AccountKey=${skillsStorageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'

@description('Portal Storage Account connection string')
#disable-next-line outputs-should-not-contain-secrets
output portalStorageConnectionString string = 'DefaultEndpointsProtocol=https;AccountName=${portalStorageAccount.name};AccountKey=${portalStorageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
