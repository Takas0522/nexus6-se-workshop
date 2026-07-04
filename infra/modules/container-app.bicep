@description('Location for the resources')
param location string

@description('Resource tags')
param tags object = {}

@description('Container App name')
param containerAppName string

@description('Container Apps Environment ID')
param environmentId string

@description('Container Registry login server')
param acrLoginServer string

@description('Environment variables for the container app')
param environmentVariables array = []

@description('Image name with tag')
param image string = 'mcr.microsoft.com/k8se/quickstart:latest'

// Container App
resource containerApp 'Microsoft.App/containerApps@2023-11-02-preview' = {
  name: containerAppName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    environmentId: environmentId
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
        allowInsecure: false
      }
      registries: contains(image, acrLoginServer) ? [
        {
          server: acrLoginServer
          identity: 'System'
        }
      ] : []
      secrets: []
    }
    template: {
      containers: [
        {
          name: containerAppName
          image: image
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: environmentVariables
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 2
      }
    }
  }
}

@description('Container App URL')
output containerAppUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'

@description('Container App principal ID (Managed Identity)')
output containerAppPrincipalId string = containerApp.identity.principalId

@description('Container App resource ID')
output containerAppId string = containerApp.id

@description('Container App FQDN')
output containerAppFqdn string = containerApp.properties.configuration.ingress.fqdn
