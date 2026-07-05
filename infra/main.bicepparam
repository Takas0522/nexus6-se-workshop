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
param keyVaultName = 'kv-nexus6-9644'

// ─── コンテナ ───
param containerRegistryName = 'crnexus69644'
param containerAppsEnvironmentName = 'cae-nexus6-9644'
param containerAppName = 'ca-nexus6-hosted-agent-9644'
param containerAppImage = 'mcr.microsoft.com/k8se/quickstart:latest'
param containerAppEnvironmentVariables = [
  { name: 'Foundry__ProjectEndpoint', value: '' }
  { name: 'Foundry__DefaultModelDeployment', value: 'gpt-5' }
  { name: 'Foundry__NotificationModelDeployment', value: 'gpt-5' }
  { name: 'Foundry__ApiVersion', value: '2024-12-01-preview' }
  { name: 'Foundry__MaxCompletionTokens', value: '16384' }
  { name: 'Foundry__AssistantsApiVersion', value: '2025-03-01-preview' }
  { name: 'Foundry__Assistant__RunMaxWaitSeconds', value: '120' }
  { name: 'Storage__Account', value: 'stnexus6skill9644' }
  { name: 'KeyVault__Uri', value: '' }
  { name: 'DevUi__EnableManualTrigger', value: 'true' }
]

// ─── ストレージ ───
param skillsStorageName = 'stnexus6skill9644'
param portalStorageName = 'stnexus6portal9644'

// ─── AI Services (Foundry) ───
param aiServicesName = 'fd-nexus6-9644'
param aiProjectName = 'proj-nexus6-9644'
param aiServicesSubdomain = 'fd-nexus6-9644'

// ─── AI Search ───
param aiSearchName = 'iq-nexus6-search-9644'

// ─── Functions ───
param appServicePlanName = 'nexus6LinuxDynPlan9644'
param functionAppName = 'func-nexus6-trigger-9644'

// ─── Fabric Capacity ───
param fabricCapacityName = 'fabricnexus69644'
param fabricCapacitySku = 'F4'
param fabricAdminMembers = []
