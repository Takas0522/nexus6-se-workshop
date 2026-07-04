using './main.bicep'

// ─── 基本設定 ───
param projectName = 'nexus6'
param location = 'swedencentral'

param tags = {
  environment: 'production'
  project: 'nexus6'
  managedBy: 'bicep'
  organization: 'nexus6-workshop'
}

// ─── 監視 ───
param logAnalyticsWorkspaceName = 'log-nexus6-swc'
param appInsightsName = 'appi-nexus6-swc'

// ─── セキュリティ ───
param keyVaultName = 'kv-nexus6-7495'

// ─── コンテナ ───
param containerRegistryName = 'crnexus67495'
param containerAppsEnvironmentName = 'cae-nexus6-7495'
param containerAppName = 'ca-nexus6-hosted-agent-7495'
param containerAppImage = 'mcr.microsoft.com/k8se/quickstart:latest'
param containerAppEnvironmentVariables = [
  { name: 'Foundry__ProjectEndpoint', value: '' }
  { name: 'Foundry__DefaultModelDeployment', value: 'gpt-5' }
  { name: 'Foundry__NotificationModelDeployment', value: 'gpt-5' }
  { name: 'Foundry__ApiVersion', value: '2024-12-01-preview' }
  { name: 'Foundry__MaxCompletionTokens', value: '16384' }
  { name: 'Foundry__AssistantsApiVersion', value: '2025-03-01-preview' }
  { name: 'Foundry__Assistant__RunMaxWaitSeconds', value: '120' }
  { name: 'Storage__Account', value: 'stnexus6skill7495' }
  { name: 'KeyVault__Uri', value: '' }
  { name: 'DevUi__EnableManualTrigger', value: 'true' }
]

// ─── ストレージ ───
param skillsStorageName = 'stnexus6skill7495'
param portalStorageName = 'stnexus6portal7495'

// ─── AI Services (Foundry) ───
param aiServicesName = 'fd-nexus6-7495'
param aiProjectName = 'proj-nexus6-7495'
param aiServicesSubdomain = 'fd-nexus6-7495'

// ─── AI Search ───
param aiSearchName = 'iq-nexus6-search-7495'

// ─── Functions ───
param appServicePlanName = 'nexus6LinuxDynPlan7495'
param functionAppName = 'func-nexus6-trigger-7495'

// ─── Fabric Capacity ───
param fabricCapacityName = 'fabricnexus67495'
param fabricCapacitySku = 'F4'
param fabricAdminMembers = ['50bfd25d-1ad5-4c55-89de-2ec91673b0a8']
