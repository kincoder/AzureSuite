@description('Azure region for the Key Vault')
param location string

@description('Globally unique name for the Key Vault')
param keyVaultName string

@description('Entra ID object id of the person/service that should get full secret access')
param principalId string

@secure()
@description('SQL admin password to store as a secret')
param sqlAdminPassword string

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    // Required for az.getSecret() references in .bicepparam files: ARM's own
    // first-party service principal resolves those at deployment time and needs
    // this flag set on the vault, separate from any RBAC role granted to a user.
    enabledForTemplateDeployment: true
  }
}

// RBAC role assignment instead of legacy access policies (the modern approach).
// "Key Vault Secrets Officer" lets this principal create/read/manage secrets.
resource secretsOfficerRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, principalId, 'KeyVaultSecretsOfficer')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7')
    principalId: principalId
    principalType: 'User'
  }
}

resource sqlAdminPasswordSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'sql-admin-password'
  properties: {
    value: sqlAdminPassword
  }
  dependsOn: [
    secretsOfficerRole
  ]
}

output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
