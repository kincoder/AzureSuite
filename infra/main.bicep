targetScope = 'resourceGroup'

param location string = resourceGroup().location
@description('Object id of the owner: SQL Entra admin and holder of the owner data-plane roles.')
param principalId string
@description('Display login of the owner, shown as the SQL Entra admin.')
param principalLogin string

module sharedAppInsights 'modules/shared/appinsights.bicep' = {
  name: 'sharedAppInsights'
  params: {
    location: location
    logAnalyticsWorkspaceName: 'law-messaginghub-dev'
    appInsightsName: 'appi-messaginghub-dev'
  }
}

var catalogKeyVaultName = 'kv-msghub-catalog-dev'
var ingestionServiceBusNamespaceName = 'sb-messaginghub-ingestion-dev'

module catalogSql 'modules/catalog/sql.bicep' = {
  name: 'catalogSql'
  params: {
    location: location
    sqlServerName: 'sql-messaginghub-catalog-dev'
    databaseName: 'catalog'
    entraAdminObjectId: principalId
    entraAdminLogin: principalLogin
  }
}

module catalogApi 'modules/catalog/appservice.bicep' = {
  name: 'catalogApi'
  params: {
    location: location
    appServicePlanName: 'asp-messaginghub-catalog-dev'
    webAppName: 'app-messaginghub-catalog-dev'
    appInsightsConnectionString: sharedAppInsights.outputs.connectionString
    sqlServerFqdn: catalogSql.outputs.sqlServerFqdn
    databaseName: 'catalog'
  }
}

module catalogKeyVault 'modules/catalog/keyvault.bicep' = {
  name: 'catalogKeyVault'
  params: {
    location: location
    keyVaultName: catalogKeyVaultName
    principalId: principalId
  }
}

module ingestionApi 'modules/ingestion/appservice.bicep' = {
  name: 'ingestionApi'
  params: {
    location: location
    appServicePlanName: 'asp-messaginghub-ingestion-dev'
    webAppName: 'app-messaginghub-ingestion-dev'
    appInsightsConnectionString: sharedAppInsights.outputs.connectionString
    serviceBusFullyQualifiedNamespace: '${ingestionServiceBusNamespaceName}.servicebus.windows.net'
    serviceBusQueueName: 'messages.raw'
  }
}

module ingestionServiceBus 'modules/ingestion/servicebus.bicep' = {
  name: 'ingestionServiceBus'
  params: {
    location: location
    serviceBusNamespaceName: ingestionServiceBusNamespaceName
    apiPrincipalId: ingestionApi.outputs.principalId
  }
}

module sharedCosmos 'modules/shared/cosmos.bicep' = {
  name: 'sharedCosmos'
  params: {
    location: location
    accountName: 'cosmos-messaginghub-dev'
  }
}

module processingFunctions 'modules/processing/functionapp.bicep' = {
  name: 'processingFunctions'
  params: {
    location: location
    appServicePlanName: 'asp-messaginghub-processing-dev'
    functionAppName: 'func-messaginghub-processing-dev'
    storageAccountName: 'stmsghubprocdev'
    appInsightsConnectionString: sharedAppInsights.outputs.connectionString
    serviceBusFullyQualifiedNamespace: ingestionServiceBus.outputs.fullyQualifiedNamespace
    inboundQueueName: ingestionServiceBus.outputs.queueName
    cosmosAccountEndpoint: sharedCosmos.outputs.accountEndpoint
    cosmosDatabaseName: sharedCosmos.outputs.databaseName
    cosmosContainerName: sharedCosmos.outputs.containerName
  }
}

// Role assignments come after both sides exist: the Function App needs the Cosmos endpoint
// and queue name at creation, and these need the Function App's identity. Keeping them in
// their own modules is what stops that from being a dependency cycle.
module processingServiceBusReceiver 'modules/processing/servicebus-receiver.bicep' = {
  name: 'processingServiceBusReceiver'
  params: {
    serviceBusNamespaceName: ingestionServiceBusNamespaceName
    queueName: ingestionServiceBus.outputs.queueName
    receiverPrincipalId: processingFunctions.outputs.principalId
  }
}

module sharedCosmosDataContributors 'modules/shared/cosmos-data-contributor.bicep' = {
  name: 'sharedCosmosDataContributors'
  params: {
    accountName: sharedCosmos.outputs.accountName
    // The Function App writes lifecycle events; principalId (you) can browse them in the
    // portal's Data Explorer, which needs a data-plane role because account keys are disabled.
    principalIds: [
      processingFunctions.outputs.principalId
      principalId
    ]
  }
}

module webStaticWebApp 'modules/web/staticwebapp.bicep' = {
  name: 'webStaticWebApp'
  params: {
    location: location
    staticWebAppName: 'stapp-messaginghub-web-dev'
  }
}

output catalogSqlServerFqdn string = catalogSql.outputs.sqlServerFqdn
output catalogKeyVaultName string = catalogKeyVault.outputs.keyVaultName
output catalogApiHostname string = catalogApi.outputs.defaultHostname
output ingestionApiHostname string = ingestionApi.outputs.defaultHostname
output webStaticWebAppHostname string = webStaticWebApp.outputs.defaultHostname
output sharedAppInsightsConnectionString string = sharedAppInsights.outputs.connectionString
output sharedCosmosAccountEndpoint string = sharedCosmos.outputs.accountEndpoint
output processingFunctionAppName string = processingFunctions.outputs.functionAppName
