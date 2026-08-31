using OperationsAgent.Api.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class DemoStageGateTests
{
    [Fact]
    public void DeterministicStartupDisablesTheAgent()
    {
        var gate = new DemoStageGate(DemoStage.Deterministic);

        Assert.False(gate.IsAgentEnabled);
        Assert.Equal(DemoStage.Deterministic, gate.GetCurrent().Id);
    }

    [Fact]
    public void InvestigationAgentStartupEnablesTheAgent()
    {
        var gate = new DemoStageGate(DemoStage.InvestigationAgent);

        Assert.True(gate.IsAgentEnabled);
    }

    [Fact]
    public void PropagatedStageChangeTogglesTheGate()
    {
        var gate = new DemoStageGate(DemoStage.Deterministic);

        gate.SetCurrent(CreateStatus(DemoStage.InvestigationAgent, DateTimeOffset.UtcNow));
        Assert.True(gate.IsAgentEnabled);

        gate.SetCurrent(CreateStatus(DemoStage.Deterministic, DateTimeOffset.UtcNow.AddSeconds(1)));
        Assert.False(gate.IsAgentEnabled);
    }

    [Fact]
    public void AnyAuthoritativeStageSupersedesTheStartupStage()
    {
        var gate = new DemoStageGate(DemoStage.Deterministic);

        // An authoritative stage applied long before this service started must still win.
        var applied = gate.SetCurrent(CreateStatus(DemoStage.Session, DateTimeOffset.UtcNow.AddHours(-2)));

        Assert.Equal(DemoStage.Session, applied.Id);
        Assert.True(gate.IsAgentEnabled);
    }

    [Fact]
    public void StaleStageDoesNotOverwriteANewerOne()
    {
        var gate = new DemoStageGate(DemoStage.Deterministic);
        var now = DateTimeOffset.UtcNow;

        gate.SetCurrent(CreateStatus(DemoStage.Session, now));
        var applied = gate.SetCurrent(CreateStatus(DemoStage.Deterministic, now.AddSeconds(-30)));

        Assert.Equal(DemoStage.Session, applied.Id);
        Assert.True(gate.IsAgentEnabled);
    }

    private static DemoStageStatus CreateStatus(DemoStage stage, DateTimeOffset appliedAt) =>
        new(stage, stage.ToString(), "Test stage", ["Test"], appliedAt, "stage-corr");
}
