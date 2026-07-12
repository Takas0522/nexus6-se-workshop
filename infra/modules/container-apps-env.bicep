@description('Location for the resources')
param location string

@description('Resource tags')
param tags object = {}

@description('Container Apps Environment name')
param environmentName string

@description('Log Analytics Workspace ID')
param logAnalyticsWorkspaceId string

// Container Apps Environment
resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2023-11-02-preview' = {
  name: environmentName
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: reference(logAnalyticsWorkspaceId, '2023-09-01').customerId
        sharedKey: listKeys(logAnalyticsWorkspaceId, '2023-09-01').primarySharedKey
      }
    }
    zoneRedundant: false
  }
}

@description('Container Apps Environment ID')
output envId string = containerAppsEnvironment.id

@description('Container Apps Environment default domain')
output envDefaultDomain string = containerAppsEnvironment.properties.defaultDomain

@description('Container Apps Environment name')
output envName string = containerAppsEnvironment.name
