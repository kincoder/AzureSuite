targetScope = 'resourceGroup'

@description('Azure region for all resources. Should match the resource group region.')
param location string = resourceGroup().location

@description('Globally unique name for the SQL logical server')
param sqlServerName string

@description('Admin login for the SQL server')
param sqlAdminLogin string

@secure()
@description('Admin password for the SQL server')
param sqlAdminPassword string

module sql 'modules/sql.bicep' = {
  name: 'sqlDeploy'
  params: {
    location: location
    sqlServerName: sqlServerName
    sqlAdminLogin: sqlAdminLogin
    sqlAdminPassword: sqlAdminPassword
  }
}

output sqlServerFqdn string = sql.outputs.sqlServerFqdn
output sqlDatabaseName string = sql.outputs.sqlDatabaseName
