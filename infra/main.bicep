@description('Project name - used for resource naming')
param projectName string = 'nexus6'

@description('Location for all resources (e.g., swedencentral)')
param location string = 'swedencentral'

@description('Resource tags to apply to all resources')
param tags object = {
  environment: 'production'
  project: projectName
  managedBy: 'bicep'
  createdDate: utcNow('yyyy-MM-dd')
}

@description('Log Analytics Workspace name')
param logAnalyticsWorkspaceName string = 'log-${projectName}-swc'

@description('Application Insights name')
param appInsightsName string = 'appi-${projectName}-swc'

@description('Key Vault name')
param keyVaultName string = 'kv-${projectName}-swc'

@description('Container Registry name')
param containerRegistryName string = 'cr${projectName}swc'

@description('Container Apps Environment name')
param containerAppsEnvironmentName string = 'cae-${projectName}-swc'

@description('Container App name')
param containerAppName string = 'ca-${projectName}-hosted-agent'

@description('Container App image')
param containerAppImage string = 'mcr.microsoft.com/k8se/quickstart:latest'

@description('Container App environment variables')
param containerAppEnvironmentVariables array = []

@description('Skills Storage Account name')
param skillsStorageName string = 'st${projectName}skill1t2i'

@description('Portal Storage Account name')
param portalStorageName string = 'st${projectName}portal1t2i'

@description('AI Services (Foundry) name')
param aiServicesName string = 'fd-${projectName}'

@description('AI Services Project name')
param aiProjectName string = 'proj-${projectName}'

@description('AI Services custom subdomain')
param aiServicesSubdomain string = 'fd-${toLower(projectName)}'

@description('AI Search service name')
param aiSearchName string = 'iq-knowledge-source'

@description('App Service Plan name')
param appServicePlanName string = '${projectName}LinuxDynamicPlan'

@description('Function App name')
param functionAppName string = 'func-${projectName}-trigger'

@description('Fabric Capacity name')
param fabricCapacityName string = 'fabric${location}cu001'

@description('Fabric Capacity SKU')
@allowed(['F2', 'F4', 'F8', 'F16', 'F32', 'F64'])
param fabricCapacitySku string = 'F4'

@description('Fabric Capacity 管理者メンバーの Object ID 配列')
param fabricAdminMembers array = []

// =====================================================================
// Monitoring Module
// =====================================================================
module monitoring './modules/monitoring.bicep' = {
  name: 'monitoring-deployment'
  params: {
    location: location
    tags: tags
    workspaceName: logAnalyticsWorkspaceName
    appInsightsName: appInsightsName
    retentionInDays: 30
  }
}

// =====================================================================
// Key Vault Module
// =====================================================================
module keyVault './modules/keyvault.bicep' = {
  name: 'keyvault-deployment'
  params: {
    location: location
    tags: tags
    vaultName: keyVaultName
  }
}

// =====================================================================
// Container Registry Module
// =====================================================================
module containerRegistry './modules/container-registry.bicep' = {
  name: 'container-registry-deployment'
  params: {
    location: location
    tags: tags
    registryName: containerRegistryName
  }
}

// =====================================================================
// Container Apps Environment Module
// =====================================================================
module containerAppsEnv './modules/container-apps-env.bicep' = {
  name: 'container-apps-env-deployment'
  params: {
    location: location
    tags: tags
    environmentName: containerAppsEnvironmentName
    logAnalyticsWorkspaceId: monitoring.outputs.workspaceId
  }
}

// =====================================================================
// Container App Module
// =====================================================================
module containerApp './modules/container-app.bicep' = {
  name: 'container-app-deployment'
  params: {
    location: location
    tags: tags
    containerAppName: containerAppName
    environmentId: containerAppsEnv.outputs.envId
    acrLoginServer: containerRegistry.outputs.acrLoginServer
    image: containerAppImage
    environmentVariables: containerAppEnvironmentVariables
  }
}

// =====================================================================
// Storage Module
// =====================================================================
module storage './modules/storage.bicep' = {
  name: 'storage-deployment'
  params: {
    location: location
    tags: tags
    skillsStorageName: skillsStorageName
    portalStorageName: portalStorageName
  }
}

// =====================================================================
// AI Services (Foundry) Module
// =====================================================================
module aiFoundry './modules/ai-foundry.bicep' = {
  name: 'ai-foundry-deployment'
  params: {
    location: location
    tags: tags
    aiServicesName: aiServicesName
    aiProjectName: aiProjectName
    customSubdomain: aiServicesSubdomain
  }
}

// =====================================================================
// AI Search Module
// =====================================================================
module aiSearch './modules/ai-search.bicep' = {
  name: 'ai-search-deployment'
  params: {
    location: location
    tags: tags
    searchName: aiSearchName
    partitions: 1
    replicas: 1
  }
}

// =====================================================================
// Functions Module
// =====================================================================
module functions './modules/functions.bicep' = {
  name: 'functions-deployment'
  params: {
    location: location
    tags: tags
    appServicePlanName: appServicePlanName
    functionAppName: functionAppName
    storageAccountName: skillsStorageName
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
  }
}

// =====================================================================
// Fabric Capacity Module
// =====================================================================
module fabric './modules/fabric.bicep' = {
  name: 'fabric-deployment'
  params: {
    name: fabricCapacityName
    location: location
    skuName: fabricCapacitySku
    administrationMembers: fabricAdminMembers
  }
}

// =====================================================================
// RBAC Module - Role assignments for Managed Identities
// =====================================================================
module rbac './modules/rbac.bicep' = {
  name: 'rbac-deployment'
  params: {
    containerAppPrincipalId: containerApp.outputs.containerAppPrincipalId
    functionAppPrincipalId: functions.outputs.functionAppPrincipalId
    acrName: containerRegistryName
    keyVaultName: keyVaultName
    aiServicesName: aiServicesName
    aiSearchName: aiSearchName
    skillsStorageName: skillsStorageName
    portalStorageName: portalStorageName
  }
}

// =====================================================================
// Diagnostics Module - ログ集約
// =====================================================================
module diagnostics './modules/diagnostics.bicep' = {
  name: 'diagnostics-deployment'
  params: {
    logAnalyticsWorkspaceId: monitoring.outputs.workspaceId
    keyVaultName: keyVaultName
    aiServicesName: aiServicesName
    aiSearchName: aiSearchName
  }
  dependsOn: [
    keyVault
    aiFoundry
    aiSearch
  ]
}

// =====================================================================
// Outputs
// =====================================================================

@description('Log Analytics Workspace ID')
output logAnalyticsWorkspaceId string = monitoring.outputs.workspaceId

@description('Log Analytics Workspace name')
output logAnalyticsWorkspaceName string = monitoring.outputs.workspaceName

@description('Application Insights connection string')
output appInsightsConnectionString string = monitoring.outputs.appInsightsConnectionString

@description('Application Insights instrumentation key')
output appInsightsInstrumentationKey string = monitoring.outputs.appInsightsInstrumentationKey

@description('Key Vault URI')
output keyVaultUri string = keyVault.outputs.vaultUri

@description('Key Vault name')
output keyVaultName string = keyVault.outputs.vaultName

@description('Container Registry login server')
output acrLoginServer string = containerRegistry.outputs.acrLoginServer

@description('Container Registry name')
output acrName string = containerRegistry.outputs.acrName

@description('Container Apps Environment ID')
output containerAppsEnvironmentId string = containerAppsEnv.outputs.envId

@description('Container Apps Environment default domain')
output containerAppsEnvironmentDefaultDomain string = containerAppsEnv.outputs.envDefaultDomain

@description('Container App URL')
output containerAppUrl string = containerApp.outputs.containerAppUrl

@description('Container App FQDN')
output containerAppFqdn string = containerApp.outputs.containerAppFqdn

@description('Skills Storage Account name')
output skillsStorageName string = storage.outputs.skillsStorageName

@description('Portal Storage Account name')
output portalStorageName string = storage.outputs.portalStorageName

@description('Portal static web endpoint')
output portalStaticWebEndpoint string = storage.outputs.portalStaticWebEndpoint

@description('AI Services endpoint')
output aiServicesEndpoint string = aiFoundry.outputs.foundryEndpoint

@description('AI Services account name')
output aiServicesName string = aiFoundry.outputs.foundryName

@description('AI Services Project name')
output aiServicesProjectName string = aiFoundry.outputs.projectName

@description('AI Search endpoint')
output aiSearchEndpoint string = aiSearch.outputs.searchEndpoint

@description('AI Search service name')
output aiSearchName string = aiSearch.outputs.searchName

@description('Function App name')
output functionAppName string = functions.outputs.functionAppName

@description('Function App default hostname')
output functionAppDefaultHostName string = functions.outputs.functionAppDefaultHostName

@description('Fabric Capacity name')
output fabricCapacityName string = fabric.outputs.fabricCapacityName

@description('Resource group location')
output deploymentLocation string = location

@description('Resource group tags')
output deploymentTags object = tags
