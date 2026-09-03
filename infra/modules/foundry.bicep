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

@description('Tags applied to the account and project.')
param tags object = {}

// The account is the security boundary that holds projects. Its system-assigned identity is what the
// platform uses for infrastructure work such as pulling the hosted agent's image from the registry -
// it is not the identity the agent itself runs as. That one is created per agent, at deploy time.
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

// Deployments are serialized rather than parallel when you add more: the account rejects concurrent
// deployment writes. Chain any second model off this one with a dependsOn.
resource modelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01' = {
  parent: account
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

@description('Resource ID of the Foundry account.')
output accountId string = account.id

@description('Foundry account name.')
output accountName string = account.name

@description('Resource ID of the project.')
output projectId string = project.id

@description('Project name.')
output projectName string = project.name

@description('Principal ID of the account identity, used by the platform to pull the agent image.')
output accountPrincipalId string = account.identity.principalId

@description('Principal ID of the project identity.')
output projectPrincipalId string = project.identity.principalId

@description('Project data-plane endpoint. This is FOUNDRY_PROJECT_ENDPOINT.')
output projectEndpoint string = '${account.properties.endpoint}api/projects/${project.name}'

@description('Model deployment name, for service configuration.')
output modelDeploymentName string = modelDeployment.name
