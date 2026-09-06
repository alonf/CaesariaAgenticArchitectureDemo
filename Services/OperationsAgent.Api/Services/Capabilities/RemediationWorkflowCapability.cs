using Microsoft.Extensions.AI;

namespace OperationsAgent.Api.Services.Capabilities;

/// <summary>
/// From the Workflow stage the corrective capability is an orchestration, not a write: the agent
/// starts the governed operation, and the workflow owns validation, policy, approval, execution
/// and verification. The direct write the previous stage held is withdrawn at the same moment.
/// </summary>
internal sealed class RemediationWorkflowCapability(RemediationWorkflowService remediationWorkflow, ILoggerFactory loggerFactory) : AgentCapability
{
    public override bool IsAvailable(DemoStage stage) => stage >= DemoStage.Workflow;

    public override ValueTask ComposeAsync(AgentComposition composition, CancellationToken cancellationToken)
    {
        var remediationTools = new RemediationTools(
            remediationWorkflow, composition.CorrelationId, loggerFactory.CreateLogger<RemediationTools>());

        composition.Tools.Add(AIFunctionFactory.Create(
            remediationTools.StartRestoreLightingOperation,
            OperationsAgentToolNames.StartRestoreLightingOperation,
            "Starts the governed Restore Lighting Operation workflow for a streetlight."));

        return ValueTask.CompletedTask;
    }
}
