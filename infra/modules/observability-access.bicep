metadata description = 'Grants the Foundry project read access to the traces its agents produce.'

// A module of its own because of an ordering circle: Application Insights must exist before the
// project can connect to it, and the project must exist before its identity can be granted
// anything. Splitting the grant out is what breaks that.

@description('Name of the Application Insights component the project reports to.')
param applicationInsightsName string

@description('Principal ID of the Foundry project identity.')
param projectPrincipalId string

// Log Analytics Reader.
var logAnalyticsReaderRoleId = '73c42c96-874c-492b-b04d-ab87d138a893'

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: applicationInsightsName
}

// Evaluation runs against the traces an agent already produced. Without this the project can write
// telemetry it is then unable to measure.
resource traceReaderForProject 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: applicationInsights
  name: guid(applicationInsights.id, projectPrincipalId, logAnalyticsReaderRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', logAnalyticsReaderRoleId)
    principalId: projectPrincipalId
    principalType: 'ServicePrincipal'
    description: 'The project reads back the traces its agents produce, for evaluation.'
  }
}
