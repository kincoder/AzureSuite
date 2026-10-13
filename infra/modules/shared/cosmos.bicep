param location string
param accountName string
param databaseName string = 'messaginghub'
param containerName string = 'LifecycleEvents'

// Free tier: the first 1000 RU/s and 25 GB of the account are free. Only one free-tier account
// is allowed per subscription, and the setting can't be changed after the account is created.
// Throughput is provisioned once at database level (shared by its containers) and kept at the
// free allowance, so this costs nothing as long as it stays at 1000.
var sharedThroughput = 1000

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' = {
  name: accountName
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    enableFreeTier: true
    // Keys disabled: every client (the Function App, you in Data Explorer) authenticates with
    // Entra ID and a data-plane role assignment (see cosmos-data-contributor.bicep).
    disableLocalAuth: true
    minimalTlsVersion: 'Tls12'
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
  }
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-11-15' = {
  parent: cosmosAccount
  name: databaseName
  properties: {
    resource: {
      id: databaseName
    }
    options: {
      throughput: sharedThroughput
    }
  }
}

resource container 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-11-15' = {
  parent: database
  name: containerName
  properties: {
    resource: {
      id: containerName
      // Every event for one message shares a logical partition, so "everything that happened
      // to message X" is a single-partition read.
      partitionKey: {
        paths: [
          '/messageId'
        ]
        kind: 'Hash'
      }
    }
  }
}

output accountName string = cosmosAccount.name
output accountEndpoint string = cosmosAccount.properties.documentEndpoint
output databaseName string = database.name
output containerName string = container.name
