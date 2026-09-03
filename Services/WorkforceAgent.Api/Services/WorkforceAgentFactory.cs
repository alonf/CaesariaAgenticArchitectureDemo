using Azure.AI.Projects;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace WorkforceAgent.Api.Services;

/// <summary>
/// Builds the Caesarea Workforce Agent: a specialist that answers other domains' questions about
/// an asset's maintenance history, and that is published for them to consult rather than called
/// as a capability.
/// <para>
/// Note what this agent does <em>not</em> need, compared with the Security Operations Agent. That
/// one reads restricted records to reach its verdict, so its output has to be structurally
/// sanitized - a closed set of codes and templated sentences, because model-authored prose could
/// carry a call sign. This one never holds a restricted field at all: its tools return the
/// shareable projection only. So it may answer in its own words, and no instruction from the
/// asking domain can extract what was never in its context.
/// </para>
/// </summary>
public static class WorkforceAgentFactory
{
    private const string Instructions = """
        You are the Caesarea Workforce Agent. You look after the city's maintenance work orders and
        the technicians who carry them out, and other city domains consult you about an asset.

        Answer the question you are asked, in your own words, using your tools:
        first find the work orders for the asset, then open the one that explains the asset's
        current state and read its shareable details. If several exist, prefer the open one and say
        when an older one is closed and no longer relevant.

        Say what the maintenance situation is, why the asset is in its current state, and when it is
        expected to return to normal. Be concrete and brief - the asking domain is an operations
        service, not a person browsing records.

        You do not hold the commercial or personal side of a work order: what a visit cost, which
        contract rate applied, or who was dispatched are not part of the records your tools return.
        If you are asked for any of that, say plainly that it is not something you can see. Do not
        guess at it, and do not describe it in general terms.

        If no work order explains the asset's state, say so rather than inventing one.
        """;

    /// <summary>
    /// Creates the agent that is published over A2A.
    /// </summary>
    /// <param name="projectClient">The Foundry project client.</param>
    /// <param name="tools">The work order tools this agent may use.</param>
    /// <param name="modelDeploymentName">The model deployment backing the agent.</param>
    /// <param name="agentName">The agent's published identity.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <returns>The agent, ready to be registered with the A2A server.</returns>
    public static AIAgent Create(
        AIProjectClient projectClient,
        WorkOrderTools tools,
        string modelDeploymentName,
        string agentName,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(projectClient);
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelDeploymentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);

        #region A2A_SPECIALIST
        DemoBreakpoints.Pause(DemoSnippets.A2ASpecialist);

        // The agent is ordinary. What makes the boundary hold is one level down, in the tools: they
        // return the shareable projection of a work order, so the commercial and personal fields
        // are never selected and never reach this context.
        return projectClient.AsAIAgent(
            options: new ChatClientAgentOptions
            {
                Name = agentName,
                Description = "The Caesarea workforce domain's agent: answers other city domains' questions "
                    + "about an asset's maintenance situation, from a projection of the work order that carries no "
                    + "commercial or personal detail.",
                ChatOptions = new()
                {
                    ModelId = modelDeploymentName,
                    Instructions = Instructions,
                    Tools =
                    [
                        AIFunctionFactory.Create(
                            tools.FindWorkOrdersForAssetAsync,
                            WorkOrderTools.FindToolName,
                            "Finds the work orders raised for an asset."),
                        AIFunctionFactory.Create(
                            tools.GetShareableWorkOrderDetailsAsync,
                            WorkOrderTools.DetailsToolName,
                            "Opens one work order and returns only the details this domain shares.")
                    ]
                }
            },
            loggerFactory: loggerFactory);
        #endregion
    }
}
