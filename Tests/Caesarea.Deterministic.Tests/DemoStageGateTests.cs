using OperationsAgent.Api.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class DemoStageGateTests
{
    [Fact]
    public void DeterministicStartupDisablesTheAgent()
    {
        var gate = new DemoStageGate(new TestTimeProvider(), DemoStage.Deterministic);

        Assert.False(gate.IsAgentEnabled);
        Assert.Equal(DemoStage.Deterministic, gate.GetCurrent().Id);
    }

    [Fact]
    public void InvestigationAgentStartupEnablesTheAgent()
    {
        var gate = new DemoStageGate(new TestTimeProvider(), DemoStage.InvestigationAgent);

        Assert.True(gate.IsAgentEnabled);
    }

    [Fact]
    public void PropagatedStageChangeTogglesTheGate()
    {
        var gate = new DemoStageGate(new TestTimeProvider(), DemoStage.Deterministic);

        gate.SetCurrent(CreateStatus(DemoStage.InvestigationAgent));
        Assert.True(gate.IsAgentEnabled);

        gate.SetCurrent(CreateStatus(DemoStage.Deterministic));
        Assert.False(gate.IsAgentEnabled);
    }

    private static DemoStageStatus CreateStatus(DemoStage stage) =>
        new(stage, stage.ToString(), "Test stage", ["Test"], DateTimeOffset.UtcNow, "stage-corr");
}
