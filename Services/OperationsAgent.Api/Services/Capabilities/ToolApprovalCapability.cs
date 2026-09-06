using Microsoft.Extensions.AI;

namespace OperationsAgent.Api.Services.Capabilities;

/// <summary>
/// The third control point. MRTR was the tool asking for input; the workflow's gate was a node in
/// an orchestration we drew. This one is reactive: the model picks a sensitive capability on its
/// own, and the framework intercepts the call so a supervisor decides before it runs. Nothing about
/// the tool itself changes. Its read-only partner travels with it: the incident lookup that finds
/// existing work before new work is filed.
/// </summary>
internal sealed class ToolApprovalCapability(
    IWorkItemGateway workItems,
    IIncidentGateway incidents,
    DemoStageGate stageGate,
    ILoggerFactory loggerFactory) : AgentCapability
{
    public override bool IsAvailable(DemoStage stage) => stage >= DemoStage.ToolApproval;

    public override ValueTask ComposeAsync(AgentComposition composition, CancellationToken cancellationToken)
    {
        var correlationId = composition.CorrelationId;

        #region TOOL_APPROVAL
        DemoBreakpoints.Pause(DemoSnippets.ToolApproval);

        var maintenanceTools = new MaintenanceTools(
            workItems, stageGate, correlationId, loggerFactory.CreateLogger<MaintenanceTools>());

        AIFunction fileWorkItem = new ApprovalRequiredAIFunction(
            AIFunctionFactory.Create(
                maintenanceTools.CreateMaintenanceWorkItem,
                OperationsAgentToolNames.CreateMaintenanceWorkItem,
                "Files a maintenance work item so a technician is dispatched to an asset."));

        composition.Tools.Add(fileWorkItem);
        #endregion

        // Existing work is found before new work is filed: the asset's state names its open
        // incident, and this lookup tells the model what that incident already covers.
        // Deliberately not wrapped - looking is not committing.
        var incidentTools = new IncidentTools(
            incidents, correlationId, loggerFactory.CreateLogger<IncidentTools>());

        composition.Tools.Add(AIFunctionFactory.Create(
            incidentTools.GetIncidentAsync,
            OperationsAgentToolNames.GetIncident,
            "Gets an incident the Command Center already tracks, so existing work is found before new work is filed."));

        return ValueTask.CompletedTask;
    }
}
