metadata description = 'Microsoft Foundry account, project and model deployment for the Caesarea agents.'

@description('Azure region. Must be one of the Hosted-agent regions, and the same region as the VNet if you add private networking later.')
param location string

@description('Foundry account name. Also becomes the custom subdomain, so it must be globally unique.')
@minLength(3)
@maxLength(48)
param accountName string

@description('Project name inside the account. Agents and their deployments live under a project.')
param projectName string

@description('Project display name shown in the Foundry portal.')
param projectDisplayName string = projectName

@description('Model deployment name. This is the value services pass as ModelDeploymentName.')
param modelDeploymentName string

@description('Model to deploy.')
param modelName string = 'gpt-5.5'

@description('Model version. Pin it: a floating version changes agent behaviour without a code change.')
param modelVersion string

@description('Deployment SKU. GlobalStandard is the usual pay-as-you-go choice.')
param modelSkuName string = 'GlobalStandard'

@description('Tokens-per-minute capacity, in thousands.')
@minValue(1)
param modelCapacity int = 50

@description('Set false together with private endpoints to take the account off the public internet.')
param publicNetworkAccess bool = true

@description('Resource ID of the Application Insights component the project reports to.')
param applicationInsightsId string

@description('Connection string of that component.')
@secure()
param applicationInsightsConnectionString string

@description('Tags applied to the account and project.')
param tags object = {}

// The account is the security boundary that holds projects. The image pull is done as the PROJECT
// identity, not this one - see modules/rbac.bicep. Neither is the identity the agent itself runs
// as: that one is created per agent, by the platform, when its first version is deployed.
resource account 'Microsoft.CognitiveServices/accounts@2025-06-01' = {
  name: accountName
  location: location
  tags: tags
  kind: 'AIServices'
  sku: {
    name: 'S0'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    // Required for the data-plane endpoint that agents and SDKs address.
    customSubDomainName: accountName
    // Azure rejects project creation on an AIServices account without this. The Bicep compiler
    // cannot see the constraint, so a clean local build says nothing about it - it surfaces at
    // deployment time, which is why this template is validated with what-if rather than a compile.
    allowProjectManagement: true
    // Keys off. Every caller authenticates as itself through Entra, so every call has an identity
    // in the audit log rather than a shared secret that anyone could be holding.
    disableLocalAuth: true
    publicNetworkAccess: publicNetworkAccess ? 'Enabled' : 'Disabled'
    networkAcls: {
      defaultAction: publicNetworkAccess ? 'Allow' : 'Deny'
    }
  }
}

resource project 'Microsoft.CognitiveServices/accounts/projects@2025-06-01' = {
  parent: account
  name: projectName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    displayName: projectDisplayName
    description: 'Caesarea Smart City agent project.'
  }
}

// The account rejects concurrent writes to its children with RequestConflict, and ARM parallelises
// anything without an explicit dependency. So every child below is chained to the previous one even
// where nothing about the data requires it. This is invisible to both a compile and a what-if -
// the first deployment is where it shows up, as "Another operation is in progress on the resource".
resource modelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01' = {
  parent: account
  dependsOn: [
    project
  ]
  name: modelDeploymentName
  sku: {
    name: modelSkuName
    capacity: modelCapacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: modelName
      version: modelVersion
    }
    versionUpgradeOption: 'NoAutoUpgrade'
  }
}

// The hosted-agent runtime is not implied by the account and project. A capability host of kind
// Agents is what provisions it, and public hosting has to be asked for by name.
//
// The API version is deliberately a preview one, and deliberately different from its siblings:
// enablePublicHostingEnvironment does not exist on the stable surface. Do not "tidy" this to match
// the resources above - hosted agents stop working, and nothing in a compile will tell you. This is
// the version Microsoft's own hosted-agent scaffolding ships, rather than the newest available.
//
// It needs no storage, thread-storage or vector-store connections. Those belong to the
// network-secured standard setup, which brings its own Storage, Cosmos DB and AI Search; a public
// hosted-agent environment asks for none of them.
resource agentsCapabilityHost 'Microsoft.CognitiveServices/accounts/capabilityHosts@2025-10-01-preview' = {
  parent: account
  name: 'agents'
  properties: {
    capabilityHostKind: 'Agents'
    enablePublicHostingEnvironment: true
  }
  dependsOn: [
    modelDeployment
  ]
}

// Creating Application Insights is not enough: the platform's automatic tracing for hosted agents
// follows the PROJECT's connection, and without this one there is nothing to follow. The agent still
// runs, and the traces the lecture points at simply do not appear.
resource applicationInsightsConnection 'Microsoft.CognitiveServices/accounts/projects/connections@2025-04-01-preview' = {
  parent: project
  name: 'application-insights'
  properties: {
    category: 'AppInsights'
    target: applicationInsightsId
    // The connection string is the credential here, which is why it arrives as a @secure()
    // parameter and is never an output of this module.
    authType: 'ApiKey'
    // isSharedToAll is deliberately not set. Setting it to true is accepted and then stored as
    // false, so the template and the resource disagree permanently and every what-if reports a
    // change that no apply can ever settle. It governs whether sibling projects may use this
    // connection, and there is one project.
    credentials: {
      key: applicationInsightsConnectionString
    }
    metadata: {
      ApiType: 'Azure'
      ResourceId: applicationInsightsId
    }
  }
  dependsOn: [
    agentsCapabilityHost
  ]
}

@description('Resource ID of the Foundry account.')
output accountId string = account.id

@description('Foundry account name.')
output accountName string = account.name

@description('Resource ID of the project.')
output projectId string = project.id

@description('Project name.')
output projectName string = project.name

@description('Principal ID of the project identity.')
output projectPrincipalId string = project.identity.principalId

// Read from the project rather than assembled from the account. An AIServices account publishes
// several endpoints on different hosts, and `account.properties.endpoint` is the Cognitive Services
// one - https://<name>.cognitiveservices.azure.com/. Building the project URL on that host produces
// a string that looks entirely correct and resolves to the wrong service; the agent data plane lives
// on https://<name>.services.ai.azure.com/. The project publishes the finished URL itself, so take
// it from there instead of reconstructing it and being right by luck.
@description('Project data-plane endpoint. This is FOUNDRY_PROJECT_ENDPOINT.')
output projectEndpoint string = project.properties.endpoints['AI Foundry API']

@description('Model deployment name, for service configuration.')
output modelDeploymentName string = modelDeployment.name
