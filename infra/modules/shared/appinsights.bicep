param location string
param logAnalyticsWorkspaceName string
param appInsightsName string
@description('Daily ingestion cap in GB. 0.15 GB a day stays inside the 5 GB monthly free allowance.')
param dailyQuotaGb string = '0.15'

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsWorkspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    // Every request is logged, rejected ones included, so a bot flood would otherwise turn
    // straight into billed ingestion. Past the cap, ingestion stops until the next UTC day.
    workspaceCapping: {
      dailyQuotaGb: json(dailyQuotaGb)
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
    IngestionMode: 'LogAnalytics'
  }
}

output connectionString string = appInsights.properties.ConnectionString
