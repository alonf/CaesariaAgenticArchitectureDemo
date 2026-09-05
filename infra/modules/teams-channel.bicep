metadata description = 'The Azure Bot that fronts the hosted agent in Microsoft Teams and the M365 Copilot agent store.'

// Reverse-engineered from what the Foundry portal's "Publish to Teams and Microsoft 365" creates,
// captured before/after with scripts/Capture-AgentChannelState.ps1 - so the Teams presence rebuilds
// with the rest of the platform instead of surviving only as a portal click.
//
// The shape the portal produced, and this reproduces:
//   - an Azure Bot (kind azurebot, SKU S1, global) whose messaging endpoint is the AGENT's own
//     Activity endpoint - Foundry fronts the Activity protocol and translates it to the Responses
//     invocations the container serves, so no Bot Framework code and no messaging host of ours;
//   - msaAppId set to the AGENT's identity (single-tenant), so the bot authenticates as the agent
//     with no stored secret and no extra app registration;
//   - the MsTeams channel enabled (WebChat and DirectLine come along by default).
//
// Runs AFTER the agent's first version exists, because the identity it references is minted then -
// the same ordering as the agent's RBAC grants. Deploy it with scripts/Publish-AgentToTeams.ps1,
// which reads the identity and endpoint back from the deployed agent.
//
// Not reproduced here: the M365 Copilot agent-store "Direct publish / Just you" registration the
// portal also performed (it flipped the agent's publish_approval_status to no_approval_needed).
// That has no confirmed ARM or data-plane route yet; the bot + Teams channel is the messaging half,
// and the store availability is documented as the remaining portal step in docs/prompts/13-agent365.md.

targetScope = 'resourceGroup'

@description('Name for the Azure Bot. A stable name, unlike the portal\'s random suffix, so redeploys converge.')
param botName string

@description('Display name shown for the bot.')
param displayName string

@description('The agent identity\'s application (client) ID - the bot authenticates as the agent. This is the agent version\'s instance_identity.principal_id.')
param agentAppId string

@description('The tenant the agent identity belongs to.')
param agentTenantId string = subscription().tenantId

@description('The agent\'s Activity-protocol messaging endpoint, e.g. https://<account>.services.ai.azure.com/api/projects/<project>/agents/<name>/endpoint/protocols/activityprotocol?api-version=2025-11-15-preview.')
param messagingEndpoint string

@description('Tags applied to the bot.')
param tags object = {}

// Bots are global, never regional - the portal creates them at location 'global' and any region
// value is rejected.
resource bot 'Microsoft.BotService/botServices@2022-09-15' = {
  name: botName
  location: 'global'
  kind: 'azurebot'
  tags: tags
  sku: {
    name: 'S1'
  }
  properties: {
    displayName: displayName
    endpoint: messagingEndpoint
    // The agent's own identity, single-tenant. No msaAppPassword: the agent identity carries its
    // own credential, which is the whole point of a per-agent identity.
    msaAppId: agentAppId
    msaAppType: 'SingleTenant'
    msaAppTenantId: agentTenantId
    schemaTransformationVersion: '1.3'
  }
}

// The channel that puts the agent in Teams. isEnabled is the switch the portal flips; declaring it
// here is what makes the Teams presence part of the infrastructure.
resource teamsChannel 'Microsoft.BotService/botServices/channels@2022-09-15' = {
  parent: bot
  name: 'MsTeamsChannel'
  location: 'global'
  properties: {
    channelName: 'MsTeamsChannel'
    properties: {
      isEnabled: true
    }
  }
}

@description('Resource ID of the bot.')
output botId string = bot.id

@description('Name of the bot.')
output botName string = bot.name
