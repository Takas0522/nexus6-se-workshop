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
resource aiServices 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' = {
  name: aiServicesName
  location: location
  tags: tags
  kind: 'AIServices'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: customSubdomain
    allowProjectManagement: true
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
resource aiProject 'Microsoft.CognitiveServices/accounts/projects@2025-06-01' = {
  name: aiProjectName
  parent: aiServices
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {}
}

// Model Deployments
@description('GPT-5 model deployment')
resource gpt5Deployment 'Microsoft.CognitiveServices/accounts/deployments@2025-04-01-preview' = {
  name: 'gpt-5'
  parent: aiServices
  sku: {
    name: 'GlobalStandard'
    capacity: 30
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-5'
      version: '2025-08-07'
    }
    raiPolicyName: 'Microsoft.Default'
  }
  dependsOn: [
    aiProject
  ]
}

@description('Text Embedding 3 Large model deployment')
resource textEmbedding3LargeDeployment 'Microsoft.CognitiveServices/accounts/deployments@2025-04-01-preview' = {
  name: 'text-embedding-3-large'
  parent: aiServices
  sku: {
    name: 'Standard'
    capacity: 100
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'text-embedding-3-large'
      version: '1'
    }
    raiPolicyName: 'Microsoft.Default'
  }
  dependsOn: [
    aiProject
    gpt5Deployment
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
