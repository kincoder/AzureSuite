param serviceBusNamespaceName string
param queueName string
param receiverPrincipalId string

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2024-01-01' existing = {
  name: serviceBusNamespaceName
}

resource queue 'Microsoft.ServiceBus/namespaces/queues@2024-01-01' existing = {
  parent: serviceBusNamespace
  name: queueName
}

// Scoped to the one queue rather than the namespace: Processing reads messages.raw (and its
// dead-letter sub-queue, which this same assignment covers) and nothing else.
resource dataReceiverRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(queue.id, receiverPrincipalId, 'ServiceBusDataReceiver')
  scope: queue
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0')
    principalId: receiverPrincipalId
    principalType: 'ServicePrincipal'
  }
}
