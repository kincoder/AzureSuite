param location string
param sqlServerName string
param databaseName string
@description('Object id of the Microsoft Entra principal that administers the server.')
param entraAdminObjectId string
@description('Display login of that principal; only the object id is used to match it.')
param entraAdminLogin string

// Microsoft Entra-only: no SQL login can connect, so the AllowAzureServices rule below (which
// admits every Azure IP, not just ours) no longer leaves a password to brute-force. The API
// connects with its managed identity and CI with its federated identity, each as a contained
// database user (see docs/infrastructure.md).
var entraAdministrator = {
  administratorType: 'ActiveDirectory'
  principalType: 'User'
  login: entraAdminLogin
  sid: entraAdminObjectId
  tenantId: subscription().tenantId
  azureADOnlyAuthentication: true
}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    // Applies when the server is first created; on an existing server the two child resources
    // below are what set the admin and switch SQL authentication off.
    administrators: entraAdministrator
    minimalTlsVersion: '1.2'
  }
}

resource entraAdmin 'Microsoft.Sql/servers/administrators@2023-08-01-preview' = {
  parent: sqlServer
  name: 'ActiveDirectory'
  properties: {
    administratorType: entraAdministrator.administratorType
    login: entraAdministrator.login
    sid: entraAdministrator.sid
    tenantId: entraAdministrator.tenantId
  }
}

resource entraOnly 'Microsoft.Sql/servers/azureADOnlyAuthentications@2023-08-01-preview' = {
  parent: sqlServer
  name: 'Default'
  properties: {
    azureADOnlyAuthentication: true
  }
  dependsOn: [
    entraAdmin
  ]
}

resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: 'GP_S_Gen5'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 1
  }
  properties: {
    useFreeLimit: true
    // Pause (never bill) once the monthly free allowance is spent.
    freeLimitExhaustionBehavior: 'AutoPause'
    autoPauseDelay: 60
  }
}

output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
