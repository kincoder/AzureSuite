param location string
param serviceBusNamespaceName string
param queueName string = 'messages.raw'
param apiPrincipalId string

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: serviceBusNamespaceName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
}

resource queue 'Microsoft.ServiceBus/namespaces/queues@2024-01-01' = {
  parent: serviceBusNamespace
  name: queueName
  properties: {}
}

resource dataSenderRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(serviceBusNamespace.id, apiPrincipalId, 'ServiceBusDataSender')
  scope: serviceBusNamespace
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39')
    principalId: apiPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output fullyQualifiedNamespace string = '${serviceBusNamespace.name}.servicebus.windows.net'
output queueName string = queue.name
