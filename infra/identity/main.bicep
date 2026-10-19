// Entra ID app registrations, app roles and assignments for the MessagingHub APIs.
// Deployed MANUALLY by the owner with their own az login (it needs Entra rights the CI
// principal deliberately doesn't have). Not referenced by ../main.bicep and excluded from
// deploy-infra.yml. Graph resources don't support what-if; re-running is idempotent
// (uniqueName keys) and fixes the occasional replication-delay failure on first run.
extension microsoftGraphV1

@description('Object id of the owner user, who receives every app role.')
param ownerPrincipalId string

@description('Origins of the web app; each gets <origin>/authentication/login-callback as a SPA redirect URI.')
param webOrigins array

// Tenant-scoped identifier URI: the api://<appId> form would need the app to reference itself.
var apiIdentifierUri = 'api://${tenant().tenantId}/messaginghub-api'
var accessAsUserScopeId = '9c7d0b32-c8f8-429b-a554-aa96cb0f8d97'
var roleIds = {
  catalogReader: 'd9abc6ed-af5a-45b4-ade8-786deaf412c0'
  catalogAdmin: '7af834bf-b716-45f6-b286-ce6689f6b3a6'
  messagesSubmit: '0eccf6ce-9b0e-4502-964d-cb5e95fe9444'
}
var microsoftGraphAppId = '00000003-0000-0000-c000-000000000000'
var graphOpenIdScopeId = '37f7f235-527c-4136-accd-4a02d197296e'
var graphProfileScopeId = '14dad69e-099b-42c9-810b-d002981feec1'
var graphOfflineAccessScopeId = '7427e0e9-2fba-42fe-b0c0-848c9e6a8182'

resource apiApp 'Microsoft.Graph/applications@v1.0' = {
  uniqueName: 'messaginghub-api'
  displayName: 'MessagingHub-Api'
  signInAudience: 'AzureADMyOrg'
  identifierUris: [apiIdentifierUri]
  api: {
    requestedAccessTokenVersion: 2
    oauth2PermissionScopes: [
      {
        id: accessAsUserScopeId
        value: 'access_as_user'
        type: 'User'
        isEnabled: true
        adminConsentDisplayName: 'Access the MessagingHub APIs'
        adminConsentDescription: 'Lets the web app call the Catalog and Ingestion APIs as the signed-in user.'
        userConsentDisplayName: 'Access the MessagingHub APIs'
        userConsentDescription: 'Lets the web app call the Catalog and Ingestion APIs on your behalf.'
      }
    ]
  }
  appRoles: [
    {
      id: roleIds.catalogReader
      value: 'Catalog.Reader'
      displayName: 'Catalog reader'
      description: 'Read message types, clients and routes; validate payloads; look up routes.'
      allowedMemberTypes: ['User', 'Application']
      isEnabled: true
    }
    {
      id: roleIds.catalogAdmin
      value: 'Catalog.Admin'
      displayName: 'Catalog admin'
      description: 'Create, update and delete message types, clients and routes.'
      allowedMemberTypes: ['User']
      isEnabled: true
    }
    {
      id: roleIds.messagesSubmit
      value: 'Messages.Submit'
      displayName: 'Message submitter'
      description: 'Submit messages to the Ingestion API.'
      allowedMemberTypes: ['User', 'Application']
      isEnabled: true
    }
  ]
}

resource apiSp 'Microsoft.Graph/servicePrincipals@v1.0' = {
  appId: apiApp.appId
}

resource webApp 'Microsoft.Graph/applications@v1.0' = {
  uniqueName: 'messaginghub-web'
  displayName: 'MessagingHub-Web'
  signInAudience: 'AzureADMyOrg'
  spa: {
    redirectUris: [for origin in webOrigins: '${origin}/authentication/login-callback']
  }
  requiredResourceAccess: [
    {
      resourceAppId: apiApp.appId
      resourceAccess: [{ id: accessAsUserScopeId, type: 'Scope' }]
    }
    {
      resourceAppId: microsoftGraphAppId
      resourceAccess: [
        { id: graphOpenIdScopeId, type: 'Scope' }
        { id: graphProfileScopeId, type: 'Scope' }
        { id: graphOfflineAccessScopeId, type: 'Scope' }
      ]
    }
  ]
}

resource webSp 'Microsoft.Graph/servicePrincipals@v1.0' = {
  appId: webApp.appId
}

resource microsoftGraphSp 'Microsoft.Graph/servicePrincipals@v1.0' existing = {
  appId: microsoftGraphAppId
}

// Tenant-wide consent, so nobody gets a consent prompt on first sign-in.
resource webToApiGrant 'Microsoft.Graph/oauth2PermissionGrants@v1.0' = {
  clientId: webSp.id
  resourceId: apiSp.id
  consentType: 'AllPrincipals'
  scope: 'access_as_user'
}

resource webToGraphGrant 'Microsoft.Graph/oauth2PermissionGrants@v1.0' = {
  clientId: webSp.id
  resourceId: microsoftGraphSp.id
  consentType: 'AllPrincipals'
  scope: 'openid profile offline_access'
}

// Stand-in producer for testing Ingestion with client credentials. Its secret is created
// outside Bicep (az ad app credential reset), since Graph Bicep can't create passwords.
resource producerApp 'Microsoft.Graph/applications@v1.0' = {
  uniqueName: 'messaginghub-testproducer'
  displayName: 'MessagingHub-TestProducer'
  signInAudience: 'AzureADMyOrg'
}

resource producerSp 'Microsoft.Graph/servicePrincipals@v1.0' = {
  appId: producerApp.appId
}

resource ownerRoleAssignments 'Microsoft.Graph/appRoleAssignedTo@v1.0' = [for role in items(roleIds): {
  appRoleId: role.value
  principalId: ownerPrincipalId
  resourceId: apiSp.id
}]

resource producerSubmitAssignment 'Microsoft.Graph/appRoleAssignedTo@v1.0' = {
  appRoleId: roleIds.messagesSubmit
  principalId: producerSp.id
  resourceId: apiSp.id
}

output tenantId string = tenant().tenantId
output apiClientId string = apiApp.appId
output apiAudience string = apiIdentifierUri
output apiScope string = '${apiIdentifierUri}/access_as_user'
output webClientId string = webApp.appId
output producerClientId string = producerApp.appId
