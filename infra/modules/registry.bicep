metadata description = 'Azure Container Registry holding the hosted agent image.'

@description('Azure region for the registry.')
param location string

@description('Globally unique registry name (alphanumeric, 5-50 characters).')
@minLength(5)
@maxLength(50)
param registryName string

@description('Registry SKU. Premium is required for private endpoints and customer-managed keys.')
@allowed(['Basic', 'Standard', 'Premium'])
param sku string = 'Standard'

@description('Set false together with a private endpoint to take the registry off the public internet.')
param publicNetworkAccess bool = true

@description('Tags applied to the registry.')
param tags object = {}

resource registry 'Microsoft.ContainerRegistry/registries@2025-04-01' = {
  name: registryName
  location: location
  tags: tags
  sku: {
    name: sku
  }
  properties: {
    // Admin user off, always. The platform pulls with a managed identity and CI pushes with a
    // federated credential; a shared registry password has no owner and no rotation story.
    adminUserEnabled: false
    publicNetworkAccess: publicNetworkAccess ? 'Enabled' : 'Disabled'
    policies: {
      // Entra-only auth for ARM-issued tokens. Foundry's image pull requires this policy enabled.
      azureADAuthenticationAsArmPolicy: {
        status: 'enabled'
      }
      retentionPolicy: {
        status: 'enabled'
        days: 30
      }
    }
  }
}

@description('Resource ID of the container registry.')
output registryId string = registry.id

@description('Registry name.')
output registryName string = registry.name

@description('Login server, for image references such as <server>/caesarea-operations:<tag>.')
output loginServer string = registry.properties.loginServer
