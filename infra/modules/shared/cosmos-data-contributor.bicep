param accountName string
param principalIds array

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' existing = {
  name: accountName
}

// Cosmos data-plane access is granted through Cosmos's own SQL role assignments, not the
// Microsoft.Authorization/roleAssignments used for Service Bus and Storage elsewhere. The
// built-in "Cosmos DB Built-in Data Contributor" role has the fixed id ...0002.
// Kept separate from cosmos.bicep so the account can be created before the Function App (which
// needs its endpoint) and the role assigned after (once the app's identity exists).
resource dataContributor 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-11-15' = [for principalId in principalIds: {
  parent: cosmosAccount
  name: guid(cosmosAccount.id, principalId, 'CosmosBuiltInDataContributor')
  properties: {
    roleDefinitionId: '${cosmosAccount.id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002'
    principalId: principalId
    scope: cosmosAccount.id
  }
}]
