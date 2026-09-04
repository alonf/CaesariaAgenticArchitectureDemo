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

@description('Principal IDs of running services that call the project data plane. Empty until those services have managed identities of their own.')
param workloadPrincipalIds array = []

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

// A second pass, because the project must exist before its identity can be granted anything, and
// the project needs Application Insights to exist before it can connect to it. Splitting the role
// assignment out is what breaks that circle.
module observabilityAccess 'modules/observability-access.bicep' = {
  scope: resourceGroup
  name: 'observability-access'
  params: {
    applicationInsightsName: 'appi-caesarea-${environmentName}'
    projectPrincipalId: foundry.outputs.projectPrincipalId
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

// The habitat for the city services the agent reads from. Provisioned here rather than with the
// apps themselves because an environment and an identity outlive any particular release, and because
// the identity must hold AcrPull before the first app tries to pull. The apps are infra/apps.bicep,
// deployed by the release pipeline.
module containerApps 'modules/containerapps.bicep' = {
  scope: resourceGroup
  name: 'container-apps'
  params: {
    location: location
    environmentName: 'cae-caesarea-${environmentName}'
    workloadIdentityName: 'id-caesarea-services-${environmentName}'
    logAnalyticsWorkspaceId: observability.outputs.workspaceId
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
    applicationInsightsId: observability.outputs.applicationInsightsId
    applicationInsightsConnectionString: observability.outputs.applicationInsightsConnectionString
    tags: tags
  }
}

module rbac 'modules/rbac.bicep' = {
  scope: resourceGroup
  name: 'rbac'
  params: {
    registryName: registry.outputs.registryName
    foundryAccountName: foundry.outputs.accountName
    foundryProjectName: foundry.outputs.projectName
    foundryProjectPrincipalId: foundry.outputs.projectPrincipalId
    deploymentPrincipalId: deploymentPrincipalId
    deploymentPrincipalType: deploymentPrincipalType
    workloadPrincipalIds: workloadPrincipalIds
    containerAppsIdentityPrincipalId: containerApps.outputs.workloadIdentityPrincipalId
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

// `az acr build` addresses the registry by name, not by login server, so the release workflow needs
// this as well as the endpoint. Deriving it by trimming '.azurecr.io' off the endpoint would work
// today and break in any cloud with a different suffix.
@description('Container registry name.')
output AZURE_CONTAINER_REGISTRY_NAME string = registry.outputs.registryName

@description('Model deployment name.')
output MODEL_DEPLOYMENT_NAME string = foundry.outputs.modelDeploymentName

// Application Insights is deliberately absent from these outputs. Its connection string carries a
// usable ingestion key, and a deployment output is readable by anyone with read on the deployment.
// The platform injects it into hosted agents on its own; nothing else in this system needs it.

@description('Container Apps environment the city services run in.')
output AZURE_CONTAINER_APPS_ENVIRONMENT_ID string = containerApps.outputs.environmentId

@description('Container Apps environment name.')
output AZURE_CONTAINER_APPS_ENVIRONMENT string = containerApps.outputs.environmentName

@description('Resource ID of the identity the city services run as.')
output AZURE_SERVICES_IDENTITY_ID string = containerApps.outputs.workloadIdentityId

@description('Client ID of that identity, used for the registry pull configuration.')
output AZURE_SERVICES_IDENTITY_CLIENT_ID string = containerApps.outputs.workloadIdentityClientId

@description('Principal ID of that identity, for role assignments made outside this template.')
output AZURE_SERVICES_IDENTITY_PRINCIPAL_ID string = containerApps.outputs.workloadIdentityPrincipalId
