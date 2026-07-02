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
param keyVaultName = 'kv-nexus6-swc'

// ─── コンテナ ───
param containerRegistryName = 'crnexus6swc'
param containerAppsEnvironmentName = 'cae-nexus6-swc'
param containerAppName = 'ca-nexus6-hosted-agent'
param containerAppImage = 'crnexus6swc.azurecr.io/nexus6-hosted-agent:latest'
param containerAppEnvironmentVariables = [
  { name: 'Foundry__ProjectEndpoint', value: 'https://fd-partneriq.cognitiveservices.azure.com/' }
  { name: 'Foundry__DefaultModelDeployment', value: 'gpt-5.4' }
  { name: 'Foundry__NotificationModelDeployment', value: 'gpt-5.4' }
  { name: 'Foundry__ApiVersion', value: '2024-12-01-preview' }
  { name: 'Foundry__MaxCompletionTokens', value: '16384' }
  { name: 'Foundry__AssistantsApiVersion', value: '2025-03-01-preview' }
  { name: 'Foundry__Assistant__RunMaxWaitSeconds', value: '120' }
  { name: 'Storage__Account', value: 'stnexus6skill1t2i' }
  { name: 'KeyVault__Uri', value: 'https://kv-nexus6-swc.vault.azure.net/' }
  { name: 'DevUi__EnableManualTrigger', value: 'true' }
]

// ─── ストレージ ───
param skillsStorageName = 'stnexus6skill1t2i'
param portalStorageName = 'stnexus6portal1t2i'

// ─── AI Services (Foundry) ───
param aiServicesName = 'fd-PartnerIQ'
param aiProjectName = 'proj-PartnerIQ'
param aiServicesSubdomain = 'fd-partneriq'

// ─── AI Search ───
param aiSearchName = 'iq-knowledge-source'

// ─── Functions ───
param appServicePlanName = 'SwedenCentralLinuxDynamicPlan'
param functionAppName = 'func-nexus6-trigger'

// ─── Fabric Capacity ───
param fabricCapacityName = 'fabricswedencu001'
param fabricCapacitySku = 'F4'
param fabricAdminMembers = []
