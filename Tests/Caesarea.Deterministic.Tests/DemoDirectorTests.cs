using DemoScenario.Api.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class DemoDirectorTests
{
    [Fact]
    public async Task ReadinessCountsOnlyWhatTheBeatStartsFrom()
    {
        // Tool Approval starts from Controller Fault and ends on Existing Incident. The second is
        // the presenter's own step, so it is reported but must not make the beat read as unready.
        var world = new DirectorWorld(DemoStage.Deterministic, ScenarioId.NormalOperation);

        var readiness = await world.Director.GetReadinessAsync(DemoStage.ToolApproval, "corr", CancellationToken.None);

        Assert.False(readiness.StageIsCurrent);
        Assert.False(readiness.Ready);
        var fault = Assert.Single(readiness.Prerequisites, status => status.Prerequisite.ScenarioId == ScenarioId.ControllerFault);
        Assert.False(fault.Satisfied);
        var tracked = Assert.Single(readiness.Prerequisites, status => status.Prerequisite.ScenarioId == ScenarioId.ExistingIncident);
        Assert.False(tracked.Satisfied);
        Assert.False(tracked.Prerequisite.AppliesAtStart);
    }

    [Fact]
    public async Task PrepareAppliesTheFixtureThenTheStageThenTheSwitches()
    {
        // The agent gates its switches by stage and a stage change resets them, so any other order
        // would either be refused or undone.
        var world = new DirectorWorld(DemoStage.Session, ScenarioId.ForgottenOverride);
        world.Switches.Values[DemoSwitch.SecurityConsult] = DemoSwitchValues.On;

        var result = await world.Director.PrepareAsync(DemoStage.MultiAgent, "corr", CancellationToken.None);

        Assert.Equal(
            ["scenario:SecurityOperation", "stage:MultiAgent", "switch:SecurityConsult=Off"],
            world.Log);
        Assert.True(result.Readiness.Ready);
        Assert.Equal(3, result.Actions.Count);
        Assert.Contains("ready", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PrepareNeverAppliesAFixtureMeantForALaterStep()
    {
        var world = new DirectorWorld(DemoStage.Deterministic, ScenarioId.NormalOperation);

        var result = await world.Director.PrepareAsync(DemoStage.ToolApproval, "corr", CancellationToken.None);

        Assert.Equal(["scenario:ControllerFault", "stage:ToolApproval"], world.Log);
        Assert.Equal(ScenarioId.ControllerFault, world.Scenarios.Current);
        Assert.True(result.Readiness.Ready);
    }

    [Fact]
    public async Task PrepareDoesNothingWhenTheBeatIsAlreadyReady()
    {
        var world = new DirectorWorld(DemoStage.McpTools, ScenarioId.ForgottenOverride);

        var result = await world.Director.PrepareAsync(DemoStage.McpTools, "corr", CancellationToken.None);

        Assert.Empty(world.Log);
        Assert.Empty(result.Actions);
        Assert.True(result.Readiness.Ready);
    }

    [Fact]
    public async Task AnUnreadableSwitchIsUnknownNotUnmetAndIsStillSetByPreparation()
    {
        var world = new DirectorWorld(DemoStage.Knowledge, ScenarioId.ForgottenOverride);
        world.Switches.FailReads = true;

        var readiness = await world.Director.GetReadinessAsync(DemoStage.Knowledge, "corr", CancellationToken.None);
        var evidence = Assert.Single(readiness.Prerequisites, status => status.Prerequisite.Switch == DemoSwitch.WorkKnowledge);
        Assert.Null(evidence.Satisfied);
        Assert.True(readiness.Ready);

        var result = await world.Director.PrepareAsync(DemoStage.Knowledge, "corr", CancellationToken.None);
        Assert.Equal(["switch:WorkKnowledge=Present"], world.Log);
        Assert.Single(result.Actions);
    }

    [Fact]
    public async Task ASwitchTheAgentRefusesIsReportedNotThrown()
    {
        var world = new DirectorWorld(DemoStage.Session, ScenarioId.ForgottenOverride);
        world.Switches.Values[DemoSwitch.ToolSource] = DemoSwitchValues.ToolSourceLocal;
        world.Switches.FailWrites = true;

        var result = await world.Director.PrepareAsync(DemoStage.InteractiveInput, "corr", CancellationToken.None);

        Assert.False(result.Readiness.Ready);
        Assert.Contains(result.Actions, action => action.Contains("could not be set", StringComparison.Ordinal));
        Assert.Contains("still unmet", result.Summary, StringComparison.Ordinal);
    }

    private sealed class DirectorWorld
    {
        public DirectorWorld(DemoStage stage, ScenarioId scenario)
        {
            Stages = new FakeStages(stage, Log);
            Scenarios = new FakeScenarios(scenario, Log);
            Switches = new FakeSwitches(Log);
            Director = new DemoDirector(new StageCatalog(), Stages, Scenarios, Switches, NullLogger<DemoDirector>.Instance);
        }

        public List<string> Log { get; } = [];

        public FakeStages Stages { get; }

        public FakeScenarios Scenarios { get; }

        public FakeSwitches Switches { get; }

        public DemoDirector Director { get; }
    }

    private sealed class FakeStages(DemoStage current, List<string> log) : IStageApplier
    {
        public DemoStage Current { get; private set; } = current;

        public Task<DemoStageStatus> GetCurrentStageAsync(string correlationId, CancellationToken cancellationToken) =>
            Task.FromResult(Status(Current));

        public Task<DemoStageChangeResult> ApplyAsync(DemoStage stage, string correlationId, CancellationToken cancellationToken)
        {
            Current = stage;
            log.Add($"stage:{stage}");
            return Task.FromResult(new DemoStageChangeResult(Status(stage), $"Stage set to {stage}."));
        }

        private static DemoStageStatus Status(DemoStage stage) =>
            new(stage, stage.ToString(), "Test stage", ["Test"], DateTimeOffset.UtcNow, "stage-corr");
    }

    private sealed class FakeScenarios(ScenarioId current, List<string> log) : IScenarioApplier
    {
        public ScenarioId Current { get; private set; } = current;

        public ScenarioStatus GetCurrentScenario() =>
            new(Current, Current.ToString(), "Test scenario", DateTimeOffset.UtcNow, "scenario-corr");

        public Task<ScenarioApplicationResult> ApplyAsync(ScenarioId scenarioId, string correlationId, CancellationToken cancellationToken)
        {
            Current = scenarioId;
            log.Add($"scenario:{scenarioId}");
            return Task.FromResult(new ScenarioApplicationResult(GetCurrentScenario(), $"{scenarioId} applied."));
        }
    }

    private sealed class FakeSwitches(List<string> log) : IOperationsAgentSwitchClient
    {
        public Dictionary<DemoSwitch, string> Values { get; } = new()
        {
            [DemoSwitch.ToolSource] = DemoSwitchValues.ToolSourceLocal,
            [DemoSwitch.SecurityConsult] = DemoSwitchValues.Off,
            [DemoSwitch.WorkKnowledge] = DemoSwitchValues.EvidencePresent,
            [DemoSwitch.AgentHabitat] = DemoSwitchValues.HabitatLocal
        };

        public bool FailReads { get; set; }

        public bool FailWrites { get; set; }

        public Task<string> GetAsync(DemoSwitch demoSwitch, string correlationId, CancellationToken cancellationToken) =>
            FailReads
                ? throw new HttpRequestException("Operations Agent unavailable.")
                : Task.FromResult(Values[demoSwitch]);

        public Task SetAsync(DemoSwitch demoSwitch, string value, string correlationId, CancellationToken cancellationToken)
        {
            if (FailWrites)
            {
                throw new HttpRequestException("The switch is disabled in the current demo stage.");
            }

            Values[demoSwitch] = value;
            log.Add($"switch:{demoSwitch}={value}");
            return Task.CompletedTask;
        }
    }
}
