using OperationsAgent.Api.Services;
using OperationsAgent.Contracts;

namespace Caesarea.Deterministic.Tests;

public sealed class StageTransitionEffectsTests
{
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
        var effects = new StageTransitionEffects(approvals, toolSource, NullLogger<StageTransitionEffects>.Instance);
        return (effects, approvals, toolSource);
    }
}
