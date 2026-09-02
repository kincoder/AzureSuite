@description('Azure region for Application Insights')
param location string

@description('Globally-scoped-enough unique name for the Application Insights resource')
param appInsightsName string

@description('Resource id of the Log Analytics workspace this workspace-based resource stores data in')
param logAnalyticsWorkspaceId string

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspaceId
    IngestionMode: 'LogAnalytics'
  }
}

output connectionString string = appInsights.properties.ConnectionString
