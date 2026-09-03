metadata description = 'Caesarea agent platform. Provisions everything that outlives a release.'

// This template deliberately stops short of deploying an agent. It creates the account, project,
// registry, observability and RBAC that a platform team owns and changes rarely; the agent version
// - image digest, CPU, environment - is an application artifact and belongs to the release
// pipeline. See infra/README.md for why that line falls where it does.

targetScope = 'subscription'

@description('Environment name. Becomes part of every resource name, so keep it short: dev, test, prod.')
@minLength(2)
@maxLength(10)
param environmentName string

@description('Azure region. Must be a Hosted-agent region.')
param location string

@description('Resource group name. Defaults to rg-caesarea-<environmentName>.')
param resourceGroupName string = 'rg-caesarea-${environmentName}'

@description('Principal ID of the CI identity that pushes images and creates agent versions. Use a federated GitHub identity, never a client secret.')
param deploymentPrincipalId string

@description('Principal type of the CI identity.')
@allowed(['ServicePrincipal', 'User', 'Group'])
param deploymentPrincipalType string = 'ServicePrincipal'

@description('Model version to pin. A floating version changes agent behaviour with no code change.')
param modelVersion string

@description('Model to deploy.')
param modelName string = 'gpt-5.5'

@description('Model deployment name, passed to services as ModelDeploymentName.')
param modelDeploymentName string = 'gpt-5.5'

@description('Set false, and add private endpoints, to take the platform off the public internet.')
param publicNetworkAccess bool = true

@description('Extra tags merged into the standard set.')
param additionalTags object = {}

// A deterministic suffix keeps globally-unique names stable across redeployments of the same
// environment, and different between environments, without anyone having to invent one.
var resourceToken = toLower(uniqueString(subscription().subscriptionId, environmentName, location))

var tags = union(
  {
    'azd-env-name': environmentName
    workload: 'caesarea-agents'
    environment: environmentName
  },
  additionalTags
)

resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-11-01' = {
  name: resourceGroupName
  location: location
  tags: tags
}

module observability 'modules/observability.bicep' = {
  scope: resourceGroup
  name: 'observability'
  params: {
    location: location
    workspaceName: 'log-caesarea-${environmentName}'
    applicationInsightsName: 'appi-caesarea-${environmentName}'
    tags: tags
  }
}

module registry 'modules/registry.bicep' = {
  scope: resourceGroup
  name: 'registry'
  params: {
    location: location
    registryName: 'crcaesarea${resourceToken}'
    publicNetworkAccess: publicNetworkAccess
    tags: tags
  }
}

module foundry 'modules/foundry.bicep' = {
  scope: resourceGroup
  name: 'foundry'
  params: {
    location: location
    accountName: 'aif-caesarea-${resourceToken}'
    projectName: 'caesarea-${environmentName}'
    projectDisplayName: 'Caesarea Smart City (${environmentName})'
    modelDeploymentName: modelDeploymentName
    modelName: modelName
    modelVersion: modelVersion
    publicNetworkAccess: publicNetworkAccess
    tags: tags
  }
}

module rbac 'modules/rbac.bicep' = {
  scope: resourceGroup
  name: 'rbac'
  params: {
    registryName: registry.outputs.registryName
    foundryAccountName: foundry.outputs.accountName
    foundryAccountPrincipalId: foundry.outputs.accountPrincipalId
    deploymentPrincipalId: deploymentPrincipalId
    deploymentPrincipalType: deploymentPrincipalType
  }
}

// Outputs are named for the environment variables the pipeline and the services consume, so the
// handoff from provisioning to deployment is a copy rather than a translation.

@description('Resource group the platform lives in.')
output AZURE_RESOURCE_GROUP string = resourceGroup.name

@description('Foundry project data-plane endpoint.')
output FOUNDRY_PROJECT_ENDPOINT string = foundry.outputs.projectEndpoint

@description('Foundry account name.')
output AZURE_AI_ACCOUNT_NAME string = foundry.outputs.accountName

@description('Foundry project name.')
output AZURE_AI_PROJECT_NAME string = foundry.outputs.projectName

@description('Container registry login server.')
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = registry.outputs.loginServer

@description('Container registry resource ID, for reusing this registry from azd.')
output AZURE_CONTAINER_REGISTRY_RESOURCE_ID string = registry.outputs.registryId

@description('Model deployment name.')
output MODEL_DEPLOYMENT_NAME string = foundry.outputs.modelDeploymentName

@description('Application Insights connection string, for services the platform does not inject into.')
output APPLICATIONINSIGHTS_CONNECTION_STRING string = observability.outputs.applicationInsightsConnectionString
