targetScope = 'resourceGroup'

param location string = resourceGroup().location
param sqlAdminLogin string
@secure()
param sqlAdminPassword string
param principalId string

module sharedAppInsights 'modules/shared/appinsights.bicep' = {
  name: 'sharedAppInsights'
  params: {
    location: location
    logAnalyticsWorkspaceName: 'law-messaginghub-dev'
    appInsightsName: 'appi-messaginghub-dev'
  }
}

var catalogKeyVaultName = 'kv-msghub-catalog-dev'

module catalogSql 'modules/catalog/sql.bicep' = {
  name: 'catalogSql'
  params: {
    location: location
    sqlServerName: 'sql-messaginghub-catalog-dev'
    sqlAdminLogin: sqlAdminLogin
    sqlAdminPassword: sqlAdminPassword
    databaseName: 'catalog'
  }
}

var catalogDbConnectionString = 'Server=tcp:${catalogSql.outputs.sqlServerFqdn},1433;Initial Catalog=catalog;User ID=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=true;'

module catalogApi 'modules/catalog/appservice.bicep' = {
  name: 'catalogApi'
  params: {
    location: location
    appServicePlanName: 'asp-messaginghub-catalog-dev'
    webAppName: 'app-messaginghub-catalog-dev'
    appInsightsConnectionString: sharedAppInsights.outputs.connectionString
    keyVaultName: catalogKeyVaultName
  }
}

module catalogKeyVault 'modules/catalog/keyvault.bicep' = {
  name: 'catalogKeyVault'
  params: {
    location: location
    keyVaultName: catalogKeyVaultName
    principalId: principalId
    apiPrincipalId: catalogApi.outputs.principalId
    connectionStringSecretValue: catalogDbConnectionString
  }
}

module catalogStaticWebApp 'modules/catalog/staticwebapp.bicep' = {
  name: 'catalogStaticWebApp'
  params: {
    location: location
    staticWebAppName: 'stapp-messaginghub-catalog-dev'
  }
}

output catalogSqlServerFqdn string = catalogSql.outputs.sqlServerFqdn
output catalogKeyVaultName string = catalogKeyVault.outputs.keyVaultName
output catalogStaticWebAppHostname string = catalogStaticWebApp.outputs.defaultHostname
output catalogApiHostname string = catalogApi.outputs.defaultHostname
output sharedAppInsightsConnectionString string = sharedAppInsights.outputs.connectionString
