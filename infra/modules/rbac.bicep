metadata description = 'Role assignments for the Caesarea agent platform. This is the file worth reading.'

// Every assignment here is least-privilege and named after the question it answers: who is allowed
// to do what, to which resource, and why.
//
// Role definition IDs are the built-in GUIDs, resolved against a tenant rather than copied - several
// of these roles were renamed in 2026 (Azure AI User became Foundry User) while keeping their IDs:
//
//   az role definition list --name "Foundry User" --query "[0].name" -o tsv

@description('Name of the container registry the hosted agent image is pulled from.')
param registryName string

@description('Name of the Foundry account.')
param foundryAccountName string

@description('Name of the Foundry project. Project-scoped roles are bound here, not at the account.')
param foundryProjectName string

@description('Principal ID of the Foundry PROJECT identity. This is what pulls the hosted agent image.')
param foundryProjectPrincipalId string

@description('Principal ID of the CI deployment identity that builds images and creates agent versions.')
param deploymentPrincipalId string

@description('Principal type of the CI identity. ServicePrincipal for a federated GitHub identity.')
@allowed(['ServicePrincipal', 'User', 'Group'])
param deploymentPrincipalType string = 'ServicePrincipal'

@description('Principal IDs of running services that call the project data plane. Empty until those services have identities.')
param workloadPrincipalIds array = []

@description('Principal ID of the identity the container apps run as. Empty to skip its assignments.')
param containerAppsIdentityPrincipalId string = ''

// ---------------------------------------------------------------------------------------------
// Built-in role definition IDs.
// ---------------------------------------------------------------------------------------------

// Pull images. Chosen over Container Registry Repository Reader deliberately: that one is an ABAC
// repository role and only means anything on a registry placed in ABAC role-assignment mode. This
// registry is not, so granting it would produce a valid assignment that confers nothing - the worst
// kind of RBAC bug, because it deploys clean and fails at image pull.
var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'

// Push images.
var acrPushRoleId = '8311e382-0749-4cb8-b61a-304f252e45ec'

// Queue an ACR Task, which is what `az acr build` does. Neither AcrPush nor Contributor-by-accident:
// the action is Microsoft.ContainerRegistry/registries/scheduleRun/action, and this is the only
// built-in role that carries it.
var containerRegistryTasksContributorRoleId = 'fb382eab-e894-4461-af04-94435c366c3f'

// Create and update agents and their versions, and assign roles to the agent identity the platform
// mints. Scoped to the project, because that is the blast radius CI needs and no more.
var foundryProjectManagerRoleId = 'eadc314b-1a2d-4efa-be10-5d325db5065e'

// Call agents and models at runtime. What a running service needs, and nothing more.
var foundryUserRoleId = '53ca6127-db72-4b80-b1b0-d745d6d5456d'

resource registry 'Microsoft.ContainerRegistry/registries@2025-04-01' existing = {
  name: registryName
}

resource foundryAccount 'Microsoft.CognitiveServices/accounts@2025-06-01' existing = {
  name: foundryAccountName

  resource project 'projects' existing = {
    name: foundryProjectName
  }
}

// The platform pulls the hosted agent's image as the PROJECT identity, not the account identity.
// Getting this wrong deploys cleanly and then fails at deploy time with image_pull_failed, which
// reads like a bad image reference and is not one.
resource registryPullForProject 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: registry
  name: guid(registry.id, foundryProjectPrincipalId, acrPullRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalId: foundryProjectPrincipalId
    principalType: 'ServicePrincipal'
    description: 'Foundry pulls the hosted agent image as the project identity.'
  }
}

// CI queues the remote build...
resource registryBuildForDeployment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: registry
  name: guid(registry.id, deploymentPrincipalId, containerRegistryTasksContributorRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', containerRegistryTasksContributorRoleId)
    principalId: deploymentPrincipalId
    principalType: deploymentPrincipalType
    description: 'CI runs az acr build, which schedules an ACR Task.'
  }
}

// ...and needs push for the resulting image, plus pull to resolve the digest it deploys by.
resource registryPushForDeployment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: registry
  name: guid(registry.id, deploymentPrincipalId, acrPushRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPushRoleId)
    principalId: deploymentPrincipalId
    principalType: deploymentPrincipalType
    description: 'CI pushes the built image and reads back its digest.'
  }
}

// CI creates agent versions against the project data plane. Project Manager is the documented
// minimum, and it carries the right to assign roles to the agent identity the platform creates -
// which is what lets the post-deploy RBAC step in the workflow succeed.
//
// Scoped to the project rather than the account: CI has no business reshaping sibling projects.
resource foundryManagerForDeployment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: foundryAccount::project
  name: guid(foundryAccount::project.id, deploymentPrincipalId, foundryProjectManagerRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', foundryProjectManagerRoleId)
    principalId: deploymentPrincipalId
    principalType: deploymentPrincipalType
    description: 'CI creates and updates hosted agent versions in this project.'
  }
}

// The running services - the Operations Agent and its peers - only ever call the data plane. They
// get Foundry User and never Project Manager: a workload that can redefine the agent it is running
// is a workload that can rewrite its own instructions.
//
// Empty until those services have managed identities of their own. They run on the presenter's
// machine today, as the developer, which is exactly the gap the Governance stage closes.
resource foundryUserForWorkloads 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in workloadPrincipalIds: {
    scope: foundryAccount::project
    name: guid(foundryAccount::project.id, principalId, foundryUserRoleId)
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', foundryUserRoleId)
      principalId: principalId
      principalType: 'ServicePrincipal'
      description: 'Runtime call access to models and agents. No authoring rights.'
    }
  }
]

// ---------------------------------------------------------------------------------------------
// What is deliberately NOT here, and why.
//
// The hosted agent's own Microsoft Entra agent identity does not exist yet. The platform creates it
// when the first agent VERSION is created, through the data plane - not through ARM. So there is no
// principal ID to bind at provisioning time, and any role that identity needs on your own resources
// has to be assigned after that call returns.
//
// That ordering is not a gap in this template. It is the seam between infrastructure and
// application: the image digest, the CPU, the environment variables and the identity that follows
// from them are all properties of a deployed version, and versions are immutable application
// artifacts. deploy-hosted-agent.yml does that half, and reads the principal back before binding.
// ---------------------------------------------------------------------------------------------

// The container apps pull their images as this identity. It is user-assigned and created ahead of
// the apps precisely so this grant can exist before the first pull is attempted - see
// modules/containerapps.bicep for why a system-assigned identity cannot work here.
resource registryPullForContainerApps 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(containerAppsIdentityPrincipalId)) {
  scope: registry
  name: guid(registry.id, containerAppsIdentityPrincipalId, acrPullRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalId: containerAppsIdentityPrincipalId
    principalType: 'ServicePrincipal'
    description: 'The Caesarea city services pull their images as this identity.'
  }
}

@description('Role assignment IDs created here, for smoke tests and drift checks.')
output assignmentIds array = [
  registryPullForProject.id
  registryBuildForDeployment.id
  registryPushForDeployment.id
  foundryManagerForDeployment.id
]
