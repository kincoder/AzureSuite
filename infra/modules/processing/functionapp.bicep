param location string
param appServicePlanName string
param functionAppName string
param storageAccountName string
param appInsightsConnectionString string
param serviceBusFullyQualifiedNamespace string
param inboundQueueName string
param cosmosAccountEndpoint string
param cosmosDatabaseName string
param cosmosContainerName string

@description('Six-field NCRONTAB (seconds first), evaluated in UTC. Default: every day at midnight.')
param housekeepingSchedule string = '0 0 0 * * *'
param housekeepingRetentionDays int = 30

var deploymentContainerName = 'app-package'

// Every Function App needs a storage account for its runtime (timer locks, host state) and,
// on Flex Consumption, for the deployment package. Shared-key access is off: the app reaches
// it with its managed identity only.
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource deploymentContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: deploymentContainerName
}

// Flex Consumption (FC1): serverless, scale-to-zero, pay per execution. The older Linux
// Consumption plan (Y1) doesn't support .NET 10 and is being retired.
resource appServicePlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: appServicePlanName
  location: location
  kind: 'functionapp'
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  properties: {
    reserved: true
  }
}

resource functionApp 'Microsoft.Web/sites@2024-04-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${storageAccount.properties.primaryEndpoints.blob}${deploymentContainerName}'
          authentication: {
            type: 'SystemAssignedIdentity'
          }
        }
      }
      scaleAndConcurrency: {
        // A hard ceiling on what a message flood can cost: Flex bills past its free grant and
        // would otherwise scale out to the default 100. One instance is plenty for this
        // workload. 512 MB is the smallest (cheapest) instance size.
        maximumInstanceCount: 1
        instanceMemoryMB: 512
      }
      // On Flex the runtime is declared here, not through FUNCTIONS_WORKER_RUNTIME /
      // FUNCTIONS_EXTENSION_VERSION app settings (which Flex rejects).
      runtime: {
        name: 'dotnet-isolated'
        version: '10.0'
      }
    }
    siteConfig: {
      minTlsVersion: '1.2'
      appSettings: [
        {
          // Identity-based host storage: only the account name, no key.
          name: 'AzureWebJobsStorage__accountName'
          value: storageAccount.name
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          // "ServiceBus" is the trigger's Connection name; the __fullyQualifiedNamespace suffix
          // tells the extension to connect with the app's managed identity.
          name: 'ServiceBus__fullyQualifiedNamespace'
          value: serviceBusFullyQualifiedNamespace
        }
        {
          name: 'InboundQueueName'
          value: inboundQueueName
        }
        {
          name: 'Cosmos__AccountEndpoint'
          value: cosmosAccountEndpoint
        }
        {
          name: 'Cosmos__DatabaseName'
          value: cosmosDatabaseName
        }
        {
          name: 'Cosmos__ContainerName'
          value: cosmosContainerName
        }
        {
          name: 'HousekeepingSchedule'
          value: housekeepingSchedule
        }
        {
          name: 'Housekeeping__RetentionDays'
          value: string(housekeepingRetentionDays)
        }
      ]
    }
  }
  dependsOn: [
    deploymentContainer
  ]
}

// Storage Blob Data Owner lets the app read its deployment package and manage the blob leases
// the Functions host uses (e.g. so only one instance runs the midnight timer).
resource storageBlobDataOwner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, functionApp.id, 'StorageBlobDataOwner')
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b')
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output functionAppName string = functionApp.name
output defaultHostname string = functionApp.properties.defaultHostName
output principalId string = functionApp.identity.principalId
