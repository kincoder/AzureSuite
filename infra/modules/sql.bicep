@description('Azure region for the SQL server and database')
param location string

@description('Name of the logical SQL server (must be globally unique)')
param sqlServerName string

@description('Name of the database')
param sqlDbName string = 'azuresuite'

@description('Admin login for the SQL server')
param sqlAdminLogin string

@secure()
@description('Admin password for the SQL server')
param sqlAdminPassword string

@description('Display name (UPN) of the Entra ID principal to set as SQL Entra admin')
param aadAdminLogin string

@description('Entra ID object id (SID) of that principal')
param aadAdminObjectId string

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
  }
}

// Serverless General Purpose tier with the Azure SQL free offer:
// 100,000 vCore-seconds and 32GB storage free per month, auto-pauses when idle.
resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: sqlDbName
  location: location
  sku: {
    name: 'GP_S_Gen5'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 1
  }
  properties: {
    autoPauseDelay: 60
    minCapacity: json('0.5')
    useFreeLimit: true
    freeLimitExhaustionBehavior: 'AutoPause'
  }
}

// Lets Azure services (App Service, Functions, etc.) reach this server without
// listing individual outbound IPs. Your own machine needs a separate rule (added below).
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Grants this Entra ID principal centralized, passwordless authentication to the
// server (via az/SSMS/EF Core "Active Directory" auth modes) alongside the existing
// SQL-auth admin login. This does not remove the SQL login - see azureADOnlyAuthentication
// if/when we want to retire SQL auth entirely.
resource aadAdmin 'Microsoft.Sql/servers/administrators@2023-08-01-preview' = {
  parent: sqlServer
  name: 'ActiveDirectory'
  properties: {
    administratorType: 'ActiveDirectory'
    login: aadAdminLogin
    sid: aadAdminObjectId
    tenantId: subscription().tenantId
  }
}

output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabase.name
