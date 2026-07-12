@description('Container App の Principal ID')
param containerAppPrincipalId string

@description('Function App の Principal ID')
param functionAppPrincipalId string

@description('デプロイ実行ユーザーの Principal ID (Step12 ファイルアップロード用)')
param deployerPrincipalId string = ''

@description('Container Registry 名')
param acrName string

@description('Key Vault 名')
param keyVaultName string

@description('AI Services 名')
param aiServicesName string

@description('AI Search 名')
param aiSearchName string

@description('Skills Storage Account 名')
param skillsStorageName string

@description('Portal Storage Account 名')
param portalStorageName string

// ─── ロール定義 ID (built-in) ───
var roleIds = {
  acrPull: '7f951dda-4ed3-4680-a7ca-43fe172d538d'
  storageBlobDataContributor: 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
  storageQueueDataContributor: '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
  storageTableDataContributor: '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
  keyVaultSecretsUser: '4633458b-17de-408a-b874-0445c86b69e6'
  // kind:AIServices では accounts/AIServices/* 名前空間が必要。
  // Foundry User は dataActions: ["Microsoft.CognitiveServices/*"] で全名前空間カバー。
  foundryUser: '53ca6127-db72-4b80-b1b0-d745d6d5456d'
  cognitiveServicesContributor: '25fbc0a9-bd7c-42a3-aa1a-3b75d497ee68'
  searchIndexDataReader: '1407120a-92aa-4202-b7e9-c0e197c71c8f'
}

// ─── 既存リソース参照 ───
resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: acrName
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource aiServices 'Microsoft.CognitiveServices/accounts@2023-10-01-preview' existing = {
  name: aiServicesName
}

resource aiSearch 'Microsoft.Search/searchServices@2023-11-01' existing = {
  name: aiSearchName
}

resource skillsStorage 'Microsoft.Storage/storageAccounts@2023-01-01' existing = {
  name: skillsStorageName
}

resource portalStorage 'Microsoft.Storage/storageAccounts@2023-01-01' existing = {
  name: portalStorageName
}

// =====================================================================
// Container App ロール割り当て
// =====================================================================

// Container App → ACR Pull
resource containerAppAcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, containerAppPrincipalId, roleIds.acrPull)
  scope: acr
  properties: {
    principalId: containerAppPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.acrPull)
  }
}

// Container App → Storage Blob Data Contributor (Skills)
resource containerAppSkillsBlob 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(skillsStorage.id, containerAppPrincipalId, roleIds.storageBlobDataContributor)
  scope: skillsStorage
  properties: {
    principalId: containerAppPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.storageBlobDataContributor)
  }
}

// Container App → Storage Table Data Contributor (Skills)
resource containerAppSkillsTable 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(skillsStorage.id, containerAppPrincipalId, roleIds.storageTableDataContributor)
  scope: skillsStorage
  properties: {
    principalId: containerAppPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.storageTableDataContributor)
  }
}

// Container App → Storage Queue Data Contributor (Skills)
resource containerAppSkillsQueue 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(skillsStorage.id, containerAppPrincipalId, roleIds.storageQueueDataContributor)
  scope: skillsStorage
  properties: {
    principalId: containerAppPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.storageQueueDataContributor)
  }
}

// Container App → Key Vault Secrets User
resource containerAppKeyVault 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, containerAppPrincipalId, roleIds.keyVaultSecretsUser)
  scope: keyVault
  properties: {
    principalId: containerAppPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.keyVaultSecretsUser)
  }
}

// Container App → Foundry User (kind:AIServices の全データプレーン操作)
resource containerAppAiServices 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(aiServices.id, containerAppPrincipalId, roleIds.foundryUser)
  scope: aiServices
  properties: {
    principalId: containerAppPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.foundryUser)
  }
}

// Container App → AI Search Index Data Reader
resource containerAppAiSearch 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(aiSearch.id, containerAppPrincipalId, roleIds.searchIndexDataReader)
  scope: aiSearch
  properties: {
    principalId: containerAppPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.searchIndexDataReader)
  }
}

// =====================================================================
// Function App ロール割り当て
// =====================================================================

// Function App → Storage Blob Data Contributor (Skills - WebJobs + データ)
resource functionAppSkillsBlob 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(functionAppPrincipalId)) {
  name: guid(skillsStorage.id, functionAppPrincipalId, roleIds.storageBlobDataContributor)
  scope: skillsStorage
  properties: {
    principalId: functionAppPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.storageBlobDataContributor)
  }
}

// Function App → Storage Table Data Contributor (Skills)
resource functionAppSkillsTable 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(functionAppPrincipalId)) {
  name: guid(skillsStorage.id, functionAppPrincipalId, roleIds.storageTableDataContributor)
  scope: skillsStorage
  properties: {
    principalId: functionAppPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.storageTableDataContributor)
  }
}

// Function App → Storage Blob Data Contributor (Portal - 静的サイト公開用)
resource functionAppPortalBlob 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(functionAppPrincipalId)) {
  name: guid(portalStorage.id, functionAppPrincipalId, roleIds.storageBlobDataContributor)
  scope: portalStorage
  properties: {
    principalId: functionAppPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.storageBlobDataContributor)
  }
}

// =====================================================================
// デプロイ実行ユーザー ロール割り当て (Step12 Foundry Files API 用)
// =====================================================================

// Deployer → Foundry User (kind:AIServices Files/Assistants API 用)
resource deployerFoundryUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(deployerPrincipalId)) {
  name: guid(aiServices.id, deployerPrincipalId, roleIds.foundryUser)
  scope: aiServices
  properties: {
    principalId: deployerPrincipalId
    principalType: 'User'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.foundryUser)
  }
}

resource deployerStorageBlobContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(deployerPrincipalId)) {
  name: guid(skillsStorage.id, deployerPrincipalId, roleIds.storageBlobDataContributor)
  scope: skillsStorage
  properties: {
    principalId: deployerPrincipalId
    principalType: 'User'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.storageBlobDataContributor)
  }
}

// Deployer → Storage Blob Data Contributor (Portal - ニュースサイトアップロード用)
resource deployerPortalBlobContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(deployerPrincipalId)) {
  name: guid(portalStorage.id, deployerPrincipalId, roleIds.storageBlobDataContributor)
  scope: portalStorage
  properties: {
    principalId: deployerPrincipalId
    principalType: 'User'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.storageBlobDataContributor)
  }
}
