targetScope = 'resourceGroup'

param location string = resourceGroup().location
param sqlAdminLogin string
@secure()
param sqlAdminPassword string
param principalId string

module catalogKeyVault 'modules/catalog/keyvault.bicep' = {
  name: 'catalogKeyVault'
  params: {
    location: location
    keyVaultName: 'kv-msghub-catalog-dev'
    principalId: principalId
  }
}

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
