using './main.bicep'

// ─── 基本設定 ───
param projectName = 'nexus6'
param location = 'northeurope'

param tags = {
  environment: 'production'
  project: 'nexus6'
  managedBy: 'bicep'
  organization: 'nexus6-workshop'
}

// ─── 監視 ───
param logAnalyticsWorkspaceName = 'log-nexus6-neu'
param appInsightsName = 'appi-nexus6-neu'

// ─── コンテナ ───
param containerAppImage = 'mcr.microsoft.com/k8se/quickstart:latest'
param containerAppEnvironmentVariables = [
  { name: 'Foundry__ProjectEndpoint', value: '' }
  { name: 'Foundry__DefaultModelDeployment', value: 'gpt-5' }
  { name: 'Foundry__NotificationModelDeployment', value: 'gpt-5' }
  { name: 'Foundry__ApiVersion', value: '2024-12-01-preview' }
  { name: 'Foundry__MaxCompletionTokens', value: '16384' }
  { name: 'Foundry__AssistantsApiVersion', value: '2025-03-01-preview' }
  { name: 'Foundry__Assistant__RunMaxWaitSeconds', value: '120' }
  { name: 'Storage__Account', value: '' }
  { name: 'KeyVault__Uri', value: '' }
  { name: 'DevUi__EnableManualTrigger', value: 'true' }
]

// ─── Fabric Capacity ───
param fabricCapacitySku = 'F4'
param fabricAdminMembers = []
