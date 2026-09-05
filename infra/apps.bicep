metadata description = 'The Caesarea city services: SmartPole simulator and Energy Hub, as container apps.'

// Separate from main.bicep on purpose. These carry image digests, which change on every release, and
// a template that owns an image cannot also be run to reconcile the platform without reverting
// whatever was deployed since. main.bicep provisions the environment and the identity; this one
// deploys the applications into it, and is only ever run by deploy-services.yml with images it has
// just built.

targetScope = 'resourceGroup'

@description('Azure region. Must match the Container Apps environment.')
param location string = resourceGroup().location

@description('Resource ID of the Container Apps environment the apps run in.')
param containerAppsEnvironmentId string

@description('Resource ID of the user-assigned identity the apps run as. It must already hold AcrPull.')
param workloadIdentityId string

@description('Client ID of that identity, used to authenticate the registry pull.')
param workloadIdentityClientId string

@description('Registry login server, e.g. crcaesarea....azurecr.io.')
param registryLoginServer string

@description('SmartPole simulator image, by digest.')
param smartPoleImage string

@description('Energy Hub image, by digest.')
param energyHubImage string

@description('Application (client) ID of the Entra app registration that represents the Energy Hub API. Its app role is what the agent is granted.')
param energyHubApiClientId string

@description('Tenant the Energy Hub accepts tokens from.')
param tenantId string = subscription().tenantId

@description('Tags applied to both apps.')
param tags object = {}

var smartPoleAppName = 'smartpole-simulator-api'
var energyHubAppName = 'energyhub-api'

// The .NET 10 aspnet base image listens on 8080.
var containerPort = 8080

resource managedEnvironment 'Microsoft.App/managedEnvironments@2025-01-01' existing = {
  name: last(split(containerAppsEnvironmentId, '/'))
}

// ---------------------------------------------------------------------------------------------
// SmartPole simulator. Internal ingress: it is the Energy Hub's device layer, and nothing outside
// the environment has any business calling it. The agent reaches it only through the Energy Hub,
// which is the point of the canonical model.
// ---------------------------------------------------------------------------------------------
resource smartPole 'Microsoft.App/containerApps@2025-01-01' = {
  name: smartPoleAppName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${workloadIdentityId}': {}
    }
  }
  properties: {
    environmentId: containerAppsEnvironmentId
    configuration: {
      ingress: {
        external: false
        targetPort: containerPort
        transport: 'auto'
        allowInsecure: false
      }
      registries: [
        {
          server: registryLoginServer
          identity: workloadIdentityId
        }
      ]
    }
    template: {
      containers: [
        {
          name: smartPoleAppName
          image: smartPoleImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'AZURE_CLIENT_ID'
              value: workloadIdentityClientId
            }
            {
              // The cloud city has no switchboard - the demo control surface below is deliberately
              // off - so the pole boots into the situation the lecture investigates: lamp ON during
              // daylight under a forgotten manual override, the exact state work order WO-8732 in
              // the presenter's OneDrive explains. The hosted agent's two sources then agree.
              name: 'SmartPoleSimulator__StartWithForgottenOverride'
              value: 'true'
            }
          ]
        }
      ]
      // Pinned to exactly one replica, like the Energy Hub below and for the same reason: the
      // simulator's device state lives in memory. Two replicas would answer from two different
      // realities, and the demo would look flaky rather than wrong.
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}

// ---------------------------------------------------------------------------------------------
// Energy Hub. External ingress, because the hosted agent runs in a Foundry sandbox that is outside
// this VNet and can only reach it over the public internet - see docs/product-status/hosted-agent.md
// for the egress evidence. External and anonymous are not the same thing: the authConfig below
// rejects unauthenticated callers at the ingress, before a request reaches the container.
// ---------------------------------------------------------------------------------------------
resource energyHub 'Microsoft.App/containerApps@2025-01-01' = {
  name: energyHubAppName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${workloadIdentityId}': {}
    }
  }
  properties: {
    environmentId: containerAppsEnvironmentId
    configuration: {
      ingress: {
        external: true
        targetPort: containerPort
        transport: 'auto'
        allowInsecure: false
      }
      registries: [
        {
          server: registryLoginServer
          identity: workloadIdentityId
        }
      ]
    }
    template: {
      containers: [
        {
          name: energyHubAppName
          image: energyHubImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'AZURE_CLIENT_ID'
              value: workloadIdentityClientId
            }
            {
              // Internal ingress is reachable inside the environment at this name. The service
              // validates it as an absolute URI, so the full FQDN is used rather than the short name.
              name: 'EnergyHubApi__SmartPoleBaseUri'
              value: 'https://${smartPoleAppName}.internal.${managedEnvironment.properties.defaultDomain}'
            }
            {
              // No admin reset, no scenario control, no MCP server, no demo breakpoints. The ingress
              // below authenticates callers but does not check which application role they hold, so
              // any valid token would otherwise reach "erase the running scenario". The hosted agent
              // needs the two reads and nothing else.
              name: 'EnergyHubApi__EnableDemoControlSurface'
              value: 'false'
            }
          ]
        }
      ]
      // One replica, deliberately. MrtrRequestStateStore is a ConcurrentDictionary in memory: a
      // second replica would issue an MRTR token that the replica handling the next call has never
      // heard of, and the failure would look like a bug in the agent.
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}

// Container Apps' built-in authentication, in validation-only mode: it checks bearer tokens and
// never runs a login redirect, so it needs no client secret. That matters - a secret here would be a
// long-lived credential with no rotation story, which is the thing this whole platform avoids.
//
// unauthenticatedClientAction: Return401 is what makes this a gate rather than decoration. Without
// it, an unauthenticated request is passed through to the container with no identity, and the admin
// and MCP surfaces this service maps would be on the open internet.
resource energyHubAuth 'Microsoft.App/containerApps/authConfigs@2025-01-01' = {
  parent: energyHub
  // The name must be exactly 'current'. Anything else is accepted and does nothing.
  name: 'current'
  properties: {
    platform: {
      enabled: true
    }
    globalValidation: {
      unauthenticatedClientAction: 'Return401'
    }
    identityProviders: {
      azureActiveDirectory: {
        enabled: true
        registration: {
          openIdIssuer: '${environment().authentication.loginEndpoint}${tenantId}/v2.0'
          clientId: energyHubApiClientId
        }
        validation: {
          // The audience the agent asks for. A token minted for anything else - Microsoft Graph, ARM,
          // another API in the same tenant - is rejected, which is the difference between "a valid
          // token" and "a token meant for this service".
          allowedAudiences: [
            'api://${energyHubApiClientId}'
          ]
        }
      }
    }
  }
}

@description('Public FQDN of the Energy Hub. This is ENERGYHUB_BASE_URI.')
output energyHubBaseUri string = 'https://${energyHub.properties.configuration.ingress.fqdn}'

@description('Internal FQDN of the SmartPole simulator, for reference.')
output smartPoleInternalUri string = 'https://${smartPoleAppName}.internal.${managedEnvironment.properties.defaultDomain}'

@description('Name of the Energy Hub container app.')
output energyHubAppName string = energyHub.name
