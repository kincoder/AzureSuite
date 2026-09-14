param location string
param appServicePlanName string
param webAppName string
param appInsightsConnectionString string
param serviceBusFullyQualifiedNamespace string
param serviceBusQueueName string

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'F1'
    tier: 'Free'
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      appCommandLine: 'dotnet Ingestion.Api.dll'
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'ServiceBus__FullyQualifiedNamespace'
          value: serviceBusFullyQualifiedNamespace
        }
        {
          name: 'ServiceBus__QueueName'
          value: serviceBusQueueName
        }
        {
          name: 'SCM_DO_BUILD_DURING_DEPLOYMENT'
          value: 'false'
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Development'
        }
      ]
    }
  }
}

output defaultHostname string = webApp.properties.defaultHostName
output principalId string = webApp.identity.principalId
