// Microsoft Fabric Capacity

@description('Fabric Capacity 名')
param name string

@description('リソースのロケーション')
param location string = resourceGroup().location

@description('SKU')
@allowed(['F2', 'F4', 'F8', 'F16', 'F32', 'F64', 'F128', 'F256', 'F512', 'F1024'])
param skuName string = 'F4'

@description('管理者メンバーの Object ID 配列')
param administrationMembers array = []

resource fabricCapacity 'Microsoft.Fabric/capacities@2023-11-01' = {
  name: name
  location: location
  sku: {
    name: skuName
    tier: 'Fabric'
  }
  properties: {
    administration: {
      members: administrationMembers
    }
  }
}

output fabricCapacityId string = fabricCapacity.id
output fabricCapacityName string = fabricCapacity.name
