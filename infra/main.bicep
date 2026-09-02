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

@description('Globally unique name for the Key Vault')
param keyVaultName string

@description('Entra ID object id of the person who should get secret access to the Key Vault')
param principalId string

@description('UPN (login name) of the Entra ID principal to set as the SQL server Entra admin')
param aadAdminLogin string

@description('Globally-scoped-enough unique name for the Log Analytics workspace')
param logAnalyticsWorkspaceName string

@description('Globally-scoped-enough unique name for the Application Insights resource')
param appInsightsName string

module sql 'modules/sql.bicep' = {
  name: 'sqlDeploy'
  params: {
    location: location
    sqlServerName: sqlServerName
    sqlAdminLogin: sqlAdminLogin
    sqlAdminPassword: sqlAdminPassword
    aadAdminLogin: aadAdminLogin
    aadAdminObjectId: principalId
  }
}

module logAnalytics 'modules/log-analytics.bicep' = {
  name: 'logAnalyticsDeploy'
  params: {
    location: location
    workspaceName: logAnalyticsWorkspaceName
  }
}

module appInsights 'modules/app-insights.bicep' = {
  name: 'appInsightsDeploy'
  params: {
    location: location
    appInsightsName: appInsightsName
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
  }
}

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyVaultDeploy'
  params: {
    location: location
    keyVaultName: keyVaultName
    principalId: principalId
    sqlAdminPassword: sqlAdminPassword
    appInsightsConnectionString: appInsights.outputs.connectionString
  }
}

output sqlServerFqdn string = sql.outputs.sqlServerFqdn
output sqlDatabaseName string = sql.outputs.sqlDatabaseName
output keyVaultName string = keyVault.outputs.keyVaultName
output keyVaultUri string = keyVault.outputs.keyVaultUri
output logAnalyticsWorkspaceId string = logAnalytics.outputs.workspaceId
