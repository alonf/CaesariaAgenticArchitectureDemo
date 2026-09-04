metadata description = 'Container Apps environment and the identity the city services run as.'

@description('Azure region. Must match the rest of the platform.')
param location string

@description('Name of the Container Apps managed environment.')
param environmentName string

@description('Name of the user-assigned identity the container apps run as.')
param workloadIdentityName string

@description('Resource ID of the Log Analytics workspace the environment sends its logs to.')
param logAnalyticsWorkspaceId string

@description('Tags applied to both resources.')
param tags object = {}

// The identity is user-assigned, not system-assigned, and that is the whole point: a container app
// cannot pull the image it was created with unless the identity holding AcrPull already exists. A
// system-assigned identity is created *by* the app, so the first revision fails to pull and the app
// arrives broken. This one is created here, granted AcrPull in modules/rbac.bicep, and handed to the
// apps in infra/apps.bicep - which is also why the apps deploy in a separate template.
resource workloadIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: workloadIdentityName
  location: location
  tags: tags
}

resource environment 'Microsoft.App/managedEnvironments@2025-01-01' = {
  name: environmentName
  location: location
  tags: tags
  properties: {
    // 'azure-monitor' rather than 'log-analytics'. The log-analytics destination is configured with
    // the workspace's shared key, which is a long-lived secret that would have to be read at
    // deployment time and lives forever in the environment's configuration. Routing through Azure
    // Monitor uses the diagnostic setting below instead, and carries no credential at all - the same
    // reason the registry has no admin user and the Foundry account has local auth disabled.
    appLogsConfiguration: {
      destination: 'azure-monitor'
    }
    workloadProfiles: [
      {
        // Consumption: scale to zero, pay per request. The services here are demo-scale and idle
        // most of the time. A dedicated profile would be the choice if cold starts mattered.
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
  }
}

// Without this, 'azure-monitor' above sends logs precisely nowhere, and does so silently: the
// environment provisions cleanly, the apps run, and every query returns empty.
resource environmentDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  scope: environment
  name: 'to-log-analytics'
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        categoryGroup: 'allLogs'
        enabled: true
      }
    ]
  }
}

@description('Resource ID of the Container Apps environment.')
output environmentId string = environment.id

@description('Name of the Container Apps environment.')
output environmentName string = environment.name

@description('Default domain, used to predict an app FQDN before the app exists.')
output defaultDomain string = environment.properties.defaultDomain

@description('Resource ID of the workload identity.')
output workloadIdentityId string = workloadIdentity.id

@description('Client ID of the workload identity, for the registry pull configuration.')
output workloadIdentityClientId string = workloadIdentity.properties.clientId

@description('Principal ID of the workload identity, for role assignments.')
output workloadIdentityPrincipalId string = workloadIdentity.properties.principalId
