@description('Location for the resources')
param location string

@description('Resource tags')
param tags object = {}

@description('AI Services (Foundry) account name')
param aiServicesName string

@description('AI Services Project name')
param aiProjectName string

@description('Custom subdomain for AI Services')
param customSubdomain string

// AI Services Account (Foundry)
resource aiServices 'Microsoft.CognitiveServices/accounts@2023-10-01-preview' = {
  name: aiServicesName
  location: location
  tags: tags
  kind: 'AIServices'
  sku: {
    name: 'S0'
  }
  properties: {
    apiProperties: {
      statisticsEnabled: false
    }
    customSubDomainName: customSubdomain
    networkAcls: {
      defaultAction: 'Allow'
    }
    publicNetworkAccess: 'Enabled'
  }
  identity: {
    type: 'SystemAssigned'
  }
}

// AI Services Project
resource aiProject 'Microsoft.CognitiveServices/accounts/projects@2024-01-01-preview' = {
  name: aiProjectName
  parent: aiServices
  properties: {
    kind: 'AIServicesProject'
  }
}

// Model Deployments
@description('GPT-5.4 model deployment with 500K TPM')
resource gpt54Deployment 'Microsoft.CognitiveServices/accounts/deployments@2024-01-01-preview' = {
  name: 'gpt-5.4'
  parent: aiServices
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-5.4'
      version: '2026-03-05'
    }
    scaleSettings: {
      scaleType: 'TokenRateLimit'
      maxTokensPerMinuteReadCapacity: 500000
    }
    raiPolicyName: 'Microsoft.Default'
  }
  dependsOn: [
    aiProject
  ]
}

@description('Text Embedding 3 Large model deployment with 120K TPM')
resource textEmbedding3LargeDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-01-01-preview' = {
  name: 'text-embedding-3-large'
  parent: aiServices
  properties: {
    model: {
      format: 'OpenAI'
      name: 'text-embedding-3-large'
      version: '1'
    }
    scaleSettings: {
      scaleType: 'TokenRateLimit'
      maxTokensPerMinuteReadCapacity: 120000
    }
    raiPolicyName: 'Microsoft.Default'
  }
  dependsOn: [
    aiProject
  ]
}

@description('Model Router deployment with 500K TPM')
resource modelRouterDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-01-01-preview' = {
  name: 'model-router'
  parent: aiServices
  properties: {
    model: {
      format: 'OpenAI'
      name: 'model-router'
      version: '2025-11-18'
    }
    scaleSettings: {
      scaleType: 'TokenRateLimit'
      maxTokensPerMinuteReadCapacity: 500000
    }
    raiPolicyName: 'Microsoft.Default'
  }
  dependsOn: [
    aiProject
  ]
}

@description('O4-mini model deployment with 500K TPM')
resource o4MiniDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-01-01-preview' = {
  name: 'o4-mini'
  parent: aiServices
  properties: {
    model: {
      format: 'OpenAI'
      name: 'o4-mini'
      version: '2025-04-16'
    }
    scaleSettings: {
      scaleType: 'TokenRateLimit'
      maxTokensPerMinuteReadCapacity: 500000
    }
    raiPolicyName: 'Microsoft.Default'
  }
  dependsOn: [
    aiProject
  ]
}

@description('AI Services endpoint')
output foundryEndpoint string = aiServices.properties.endpoint

@description('AI Services account name')
output foundryName string = aiServices.name

@description('AI Services account ID')
output foundryId string = aiServices.id

@description('AI Services Project name')
output projectName string = aiProject.name

@description('AI Services custom subdomain')
output customSubdomain string = customSubdomain
