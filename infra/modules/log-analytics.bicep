@description('Azure region for the workspace')
param location string

@description('Globally-scoped-enough unique name for the Log Analytics workspace')
param workspaceName string

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: workspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    // Free grant: 5 GB/month ingestion + first 31 days retention, no special "Free" SKU needed.
    retentionInDays: 31
  }
}

output workspaceId string = workspace.id
