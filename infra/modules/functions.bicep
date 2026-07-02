@description('Location for the resources')
param location string

@description('Resource tags')
param tags object = {}

@description('App Service Plan name')
param appServicePlanName string

@description('Function App name')
param functionAppName string

@description('Storage Account 名 (Functions ランタイム用、MI ベース)')
param storageAccountName string

@description('Application Insights 接続文字列')
param appInsightsConnectionString string = ''

// App Service Plan - Y1 Dynamic (Linux)
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: appServicePlanName
  location: location
  tags: tags
  kind: 'linux'
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
    size: 'Y1'
    family: 'Y'
    capacity: 0
  }
  properties: {
    reserved: true
  }
}

// Function App
resource functionApp 'Microsoft.Web/sites@2023-01-01' = {
  name: functionAppName
  location: location
  tags: tags
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      use32BitWorkerProcess: false
      http20Enabled: true
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'AzureWebJobsStorage__accountName'
          value: storageAccountName
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
      ]
    }
  }
}

@description('Function App name')
output functionAppName string = functionApp.name

@description('Function App default hostname')
output functionAppDefaultHostName string = functionApp.properties.defaultHostName

@description('Function App resource ID')
output functionAppId string = functionApp.id

@description('Function App principal ID (Managed Identity)')
output functionAppPrincipalId string = functionApp.identity.principalId

@description('Function App SCM endpoint')
output functionAppScmEndpoint string = 'https://${functionApp.name}.scm.azurewebsites.net'

@description('App Service Plan ID')
output appServicePlanId string = appServicePlan.id
