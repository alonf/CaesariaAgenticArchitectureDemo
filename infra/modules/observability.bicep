metadata description = 'Log Analytics and Application Insights for the Caesarea agent platform.'

@description('Azure region for the workspace and its Application Insights component.')
param location string

@description('Name of the Log Analytics workspace.')
param workspaceName string

@description('Name of the Application Insights component.')
param applicationInsightsName string

@description('Retention in days. 30 is the free tier; raise it for anything you intend to audit.')
@minValue(30)
@maxValue(730)
param retentionInDays int = 30

@description('Tags applied to both resources.')
param tags object = {}

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: workspaceName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: retentionInDays
    features: {
      // Workspace-scoped RBAC. Without this, anyone with read on the workspace reads every table,
      // and hosted-agent traces can carry prompt content.
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

// Hosted agents receive their Application Insights connection string from the platform, injected as
// an environment variable.
//
// Local auth is deliberately NOT disabled here, unlike on the Foundry account. The project's
// AppInsights connection authenticates with this component's key, so turning local auth off would
// leave the connection valid-looking and the traces absent - a failure with no error anywhere. The
// connection string is therefore a real credential: it is passed as a @secure() parameter and is
// not a deployment output.
resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: applicationInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

@description('Resource ID of the Log Analytics workspace.')
output workspaceId string = workspace.id

@description('Resource ID of the Application Insights component.')
output applicationInsightsId string = applicationInsights.id

@description('Application Insights connection string. A credential: consumed by the project connection, never surfaced as a deployment output above this module.')
@secure()
output applicationInsightsConnectionString string = applicationInsights.properties.ConnectionString
