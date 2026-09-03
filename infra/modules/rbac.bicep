metadata description = 'Role assignments for the Caesarea agent platform. This is the file worth reading.'

// Every assignment here is least-privilege and named after the question it answers:
// who is allowed to do what, to which resource, and why. Role definition IDs are the built-in
// GUIDs - resolve them yourself with `az role definition list --name "<role>"` rather than
// trusting a copied constant, because several of these roles were renamed in 2026 (Azure AI User
// became Foundry User, and so on) while keeping their IDs.

@description('Name of the container registry the hosted agent image is pulled from.')
param registryName string

@description('Name of the Foundry account.')
param foundryAccountName string

@description('Principal ID of the Foundry account identity. The platform pulls the agent image as this.')
param foundryAccountPrincipalId string

@description('Principal ID of the CI deployment identity that pushes images and creates agent versions.')
param deploymentPrincipalId string

@description('Principal type of the CI identity. ServicePrincipal for a federated GitHub identity.')
@allowed(['ServicePrincipal', 'User', 'Group'])
param deploymentPrincipalType string = 'ServicePrincipal'

@description('Optional principal IDs of the running services that call the project data plane.')
param workloadPrincipalIds array = []

// ---------------------------------------------------------------------------------------------
// Built-in role definition IDs, resolved against the tenant rather than copied from a blog post.
// ---------------------------------------------------------------------------------------------

// Lets the puller read repositories. Narrower than AcrPull's ancestor roles and the one the
// Foundry docs name for the image pull.
var containerRegistryRepositoryReaderRoleId = 'b93aa761-3e63-49ed-ac28-beffa264f7ac'

// The classic pull role. Assigned to the agent identity post-deploy, not here - see the note below.
var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'

// Create and update agents, and create role assignments for the agent identity the platform makes.
var foundryProjectManagerRoleId = 'eadc314b-1a2d-4efa-be10-5d325db5065e'

// Call agents and models at runtime. This is what a running service needs, and nothing more.
var foundryUserRoleId = '53ca6127-db72-4b80-b1b0-d745d6d5456d'

resource registry 'Microsoft.ContainerRegistry/registries@2025-04-01' existing = {
  name: registryName
}

resource foundryAccount 'Microsoft.CognitiveServices/accounts@2025-06-01' existing = {
  name: foundryAccountName
}

// The platform pulls the hosted agent's image as the Foundry account identity. Without this the
// deployment fails with image_pull_failed, which reads like a bad image reference and is not one.
resource registryPullForFoundry 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: registry
  name: guid(registry.id, foundryAccountPrincipalId, containerRegistryRepositoryReaderRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', containerRegistryRepositoryReaderRoleId)
    principalId: foundryAccountPrincipalId
    principalType: 'ServicePrincipal'
    description: 'Foundry pulls the hosted agent image as the account identity.'
  }
}

// CI pushes images. AcrPull is deliberately not enough - see the push role assignment below.
resource registryPullForDeployment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: registry
  name: guid(registry.id, deploymentPrincipalId, acrPullRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalId: deploymentPrincipalId
    principalType: deploymentPrincipalType
    description: 'CI reads image manifests to resolve digests before deploying by digest.'
  }
}

// CI creates agent versions against the project data plane. Project Manager is the documented
// minimum for that, and it also carries the right to assign roles to the agent identity the
// platform creates - which is why the post-deploy RBAC step in the workflow can succeed.
resource foundryManagerForDeployment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: foundryAccount
  name: guid(foundryAccount.id, deploymentPrincipalId, foundryProjectManagerRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', foundryProjectManagerRoleId)
    principalId: deploymentPrincipalId
    principalType: deploymentPrincipalType
    description: 'CI creates and updates hosted agent versions.'
  }
}

// The running services - the Operations Agent and its peers - only ever call the data plane.
// They get Foundry User and never Project Manager: a workload that can redefine the agent it is
// running is a workload that can rewrite its own instructions.
resource foundryUserForWorkloads 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for (principalId, index) in workloadPrincipalIds: {
    scope: foundryAccount
    name: guid(foundryAccount.id, principalId, foundryUserRoleId)
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
// The hosted agent's own Microsoft Entra agent identity does not exist yet. The platform creates
// it when the first agent VERSION is created, through the data plane - not through ARM. So there
// is no principal ID to bind at provisioning time, and any role that identity needs on your own
// resources (a storage account, a database) has to be assigned after that call returns.
//
// That ordering is not a gap in this template. It is the seam between infrastructure and
// application: the image tag, the CPU, the environment variables and the identity that follows
// from them are all properties of a deployed version, and versions are immutable application
// artifacts. deploy-hosted-agent.yml does that half, and reads the principal back before binding.
// ---------------------------------------------------------------------------------------------

@description('Role assignment IDs created here, for smoke tests and drift checks.')
output assignmentIds array = [
  registryPullForFoundry.id
  registryPullForDeployment.id
  foundryManagerForDeployment.id
]
