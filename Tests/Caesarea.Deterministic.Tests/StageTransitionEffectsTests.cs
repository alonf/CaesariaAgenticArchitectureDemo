using OperationsAgent.Api.Services;
using OperationsAgent.Contracts;

namespace Caesarea.Deterministic.Tests;

public sealed class StageTransitionEffectsTests
{
    [Fact]
    public async Task MovingForwardIntoWorkflowWithdrawsAParkedDirectWriteConfirmation()
    {
        // The direct write exists only below Workflow, where the governed operation replaces it.
        // A confirmation parked at InteractiveInput and answered after the crossing would perform
        // exactly the write the stage took away, so the crossing refuses it.
        var (effects, approvals, _) = CreateEffects();
        var (id, decision) = approvals.Create(
            "Restore L-417 to scheduled mode?",
            "mrtr-corr",
            CancellationToken.None,
            OperationsAgentControlPoint.InteractiveInput,
            OperationsAgentToolNames.RestoreScheduledMode);

        effects.Apply(DemoStage.InteractiveInput, DemoStage.Workflow);

        // Refused, not cancelled: the run is still legitimate, so the tool is told no and the
        // agent reports it rather than the whole turn failing on a cancelled wait. Bounded, so a
        // regression that simply leaves the confirmation parked fails here instead of hanging.
        Assert.False(await decision.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.Empty(approvals.GetAll());

        // And the operator can no longer answer it approve after the fact.
        Assert.False(approvals.TryRespond(id, approved: true));
    }

    [Fact]
    public async Task AWorkflowGateIsNotDisturbedByTheSameCrossing()
    {
        // Only the interactive-input control point loses its capability at Workflow.
        var (effects, approvals, _) = CreateEffects();
        var (_, decision) = approvals.Create(
            "Remediation workflow: restore L-417?",
            "wf-corr",
            CancellationToken.None,
            OperationsAgentControlPoint.WorkflowGate,
            OperationsAgentToolNames.RestoreScheduledMode);

        effects.Apply(DemoStage.InteractiveInput, DemoStage.Workflow);

        Assert.Single(approvals.GetAll());
        Assert.False(decision.IsCompleted);

        Assert.True(approvals.TryRespond(approvals.GetAll()[0].Id, approved: true));
        Assert.True(await decision);
    }

    [Fact]
    public async Task AForwardMoveInsideTheWriteWindowLeavesTheConfirmationStanding()
    {
        var (effects, approvals, _) = CreateEffects();
        var (id, decision) = approvals.Create(
            "Restore L-417 to scheduled mode?",
            "mrtr-corr",
            CancellationToken.None,
            OperationsAgentControlPoint.InteractiveInput,
            OperationsAgentToolNames.RestoreScheduledMode);

        // McpTools to InteractiveInput adds the capability; it does not take it away.
        effects.Apply(DemoStage.McpTools, DemoStage.InteractiveInput);

        Assert.False(decision.IsCompleted);
        Assert.True(approvals.TryRespond(id, approved: true));
        Assert.True(await decision);
    }

    [Fact]
    public async Task DowngradeCancelsPendingApprovalsAndResetsTheToolSource()
    {
        var (effects, approvals, toolSource) = CreateEffects();
        toolSource.Current = OperationsAgentToolSource.Mcp;
        var (_, decision) = approvals.Create("Restore L-417?", "downgrade-corr", CancellationToken.None);

        effects.Apply(previous: DemoStage.InteractiveInput, current: DemoStage.Knowledge);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => decision);
        Assert.Empty(approvals.GetAll());
        Assert.Equal(OperationsAgentToolSource.Local, toolSource.Current);
    }

    [Fact]
    public void DowngradeWithinMcpStagesKeepsTheToolSource()
    {
        var (effects, _, toolSource) = CreateEffects();
        toolSource.Current = OperationsAgentToolSource.Mcp;

        effects.Apply(previous: DemoStage.InteractiveInput, current: DemoStage.McpTools);

        Assert.Equal(OperationsAgentToolSource.Mcp, toolSource.Current);
    }

    [Fact]
    public async Task ForwardOrUnchangedTransitionsAreNoOps()
    {
        var (effects, approvals, toolSource) = CreateEffects();
        toolSource.Current = OperationsAgentToolSource.Mcp;
        var (id, decision) = approvals.Create("Restore L-417?", "forward-corr", CancellationToken.None);

        effects.Apply(previous: DemoStage.McpTools, current: DemoStage.InteractiveInput);
        effects.Apply(previous: DemoStage.InteractiveInput, current: DemoStage.InteractiveInput);

        Assert.Contains(approvals.GetAll(), approval => approval.Id == id);
        Assert.Equal(OperationsAgentToolSource.Mcp, toolSource.Current);

        Assert.True(approvals.TryRespond(id, approved: true));
        Assert.True(await decision);
    }

    private static (StageTransitionEffects Effects, PendingApprovalStore Approvals, ToolSourceSwitch ToolSource) CreateEffects()
    {
        var approvals = new PendingApprovalStore(new TestTimeProvider(), NullLogger<PendingApprovalStore>.Instance);
        var toolSource = new ToolSourceSwitch();
        var effects = new StageTransitionEffects(
            approvals, toolSource, CreateWorkflowService(approvals), new SecurityConsultSwitch(), NullLogger<StageTransitionEffects>.Instance);
        return (effects, approvals, toolSource);
    }

    internal static RemediationWorkflowService CreateWorkflowService(PendingApprovalStore approvals) =>
        new(
            new FakeEnergyReadGateway { State = CreateTwin(), Activity = [] },
            new FakeEnergyCommandGateway(),
            new FakeWorkItemGateway(),
            approvals,
            new DemoStageGate(DemoStage.Workflow),
            TimeProvider.System,
            NullLoggerFactory.Instance,
            NullLogger<RemediationWorkflowService>.Instance);

    private static EnergyOperationalTwin CreateTwin() => new(
        DemoAssets.StreetlightAssetId,
        DemoAssets.NorthPromenadeArea,
        ReportedIsOn: false,
        DesiredIsOn: false,
        IsDaylight: true,
        ExpectedScheduledState: false,
        ManualOverride: false,
        ControllerHealthInfo.Healthy,
        LastCommand: null,
        LastMaintenanceTime: null,
        HasRecentMaintenance: false,
        OpenIncidentId: null,
        LastReportedAt: DateTimeOffset.UtcNow,
        OperationalContext.None);
}
