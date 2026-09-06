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
    public async Task AFixtureTheCityHasDriftedFromIsUnmetAndAppliedAgain()
    {
        // The deterministic beat ends with the operator restoring L-417. The scenario still reads
        // Lights On, but the next beat starts with the lamp off unless the fixture is re-applied -
        // so the identifier alone is not a fixture; the asset is.
        var world = new DirectorWorld(DemoStage.Deterministic, ScenarioId.ForgottenOverride);
        world.Energy.Twin = world.Energy.Twin with { ReportedIsOn = false, DesiredIsOn = false, ManualOverride = false };

        var readiness = await world.Director.GetReadinessAsync(DemoStage.InvestigationAgent, "corr", CancellationToken.None);
        var fixture = Assert.Single(readiness.Prerequisites, status => status.Prerequisite.Kind == DemoPrerequisiteKind.Scenario);
        Assert.False(fixture.Satisfied);
        Assert.Contains("now off", fixture.CurrentValue, StringComparison.Ordinal);

        var result = await world.Director.PrepareAsync(DemoStage.InvestigationAgent, "corr", CancellationToken.None);
        Assert.Equal(["scenario:ForgottenOverride", "stage:InvestigationAgent"], world.Log);
        Assert.True(result.Readiness.Ready);
    }

    [Fact]
    public async Task AFailedApplicationIsNotAFixture()
    {
        // An application that failed halfway keeps the identifier it asked for. The identifier
        // matching is not the city being in that state.
        var world = new DirectorWorld(DemoStage.Session, ScenarioId.ForgottenOverride);
        world.Scenarios.ApplicationStatus = ScenarioApplicationStatus.Failed;

        var readiness = await world.Director.GetReadinessAsync(DemoStage.Session, "corr", CancellationToken.None);

        var fixture = Assert.Single(readiness.Prerequisites, status => status.Prerequisite.Kind == DemoPrerequisiteKind.Scenario);
        Assert.False(fixture.Satisfied);
        Assert.Contains("Failed", fixture.CurrentValue, StringComparison.Ordinal);
        Assert.False(readiness.Ready);
    }

    [Fact]
    public async Task AnUnreadableFixtureIsUnknownAndKeepsTheBeatFromReadingAsReady()
    {
        var world = new DirectorWorld(DemoStage.Session, ScenarioId.ForgottenOverride);
        world.Energy.FailReads = true;

        var readiness = await world.Director.GetReadinessAsync(DemoStage.Session, "corr", CancellationToken.None);
        var fixture = Assert.Single(readiness.Prerequisites, status => status.Prerequisite.Kind == DemoPrerequisiteKind.Scenario);
        Assert.Null(fixture.Satisfied);
        Assert.False(readiness.Ready);
        Assert.Single(readiness.Unverified);

        // Unknown is not unmet: the fixture is not applied again on a guess, and the outcome says
        // what could not be checked.
        var result = await world.Director.PrepareAsync(DemoStage.Session, "corr", CancellationToken.None);
        Assert.Empty(world.Log);
        Assert.Contains("could not be verified", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnreadableSwitchIsUnknownNotMetAndIsStillSetByPreparation()
    {
        var world = new DirectorWorld(DemoStage.Knowledge, ScenarioId.ForgottenOverride);
        world.Switches.FailReads = true;

        var readiness = await world.Director.GetReadinessAsync(DemoStage.Knowledge, "corr", CancellationToken.None);
        var evidence = Assert.Single(readiness.Prerequisites, status => status.Prerequisite.Switch == DemoSwitch.WorkKnowledge);
        Assert.Null(evidence.Satisfied);
        Assert.False(readiness.Ready);
        Assert.Contains(readiness.Unverified, status => status.Prerequisite.Switch == DemoSwitch.WorkKnowledge);

        // The value it needs is known even when the value it has is not, so it is set - and the
        // beat still does not read as ready while the read keeps failing.
        var result = await world.Director.PrepareAsync(DemoStage.Knowledge, "corr", CancellationToken.None);
        Assert.Equal(["switch:WorkKnowledge=Present"], world.Log);
        Assert.Single(result.Actions);
        Assert.False(result.Readiness.Ready);
        Assert.Contains("could not be verified", result.Summary, StringComparison.Ordinal);
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
            Catalog = new ScenarioCatalog(TimeProvider.System);
            Energy = new FakeEnergy(TwinFor(Catalog.GetRecipe(scenario)));
            Stages = new FakeStages(stage, Log);
            // Applying a scenario moves the city: the fake energy hub follows the recipe, as the real one does.
            Scenarios = new FakeScenarios(scenario, Log, applied => Energy.Twin = TwinFor(Catalog.GetRecipe(applied)));
            Switches = new FakeSwitches(Log);
            Director = new DemoDirector(new StageCatalog(), Catalog, Stages, Scenarios, Switches, Energy, NullLogger<DemoDirector>.Instance);
        }

        public List<string> Log { get; } = [];

        public ScenarioCatalog Catalog { get; }

        public FakeEnergy Energy { get; }

        public FakeStages Stages { get; }

        public FakeScenarios Scenarios { get; }

        public FakeSwitches Switches { get; }

        public DemoDirector Director { get; }

        private static EnergyOperationalTwin TwinFor(ScenarioRecipe recipe)
        {
            var state = recipe.SmartPoleState;
            return new EnergyOperationalTwin(
                DemoAssets.StreetlightAssetId,
                DemoAssets.NorthPromenadeArea,
                state.IsOn,
                state.IsOn,
                state.IsDaylight,
                state.ExpectedScheduledState,
                state.ManualOverride,
                state.ControllerHealth,
                null,
                state.LastMaintenanceTime,
                state.HasRecentMaintenance,
                recipe.EnergyState.OpenIncidentId,
                DateTimeOffset.UtcNow,
                state.OperationContext);
        }
    }

    private sealed class FakeEnergy(EnergyOperationalTwin twin) : IEnergyScenarioClient
    {
        public EnergyOperationalTwin Twin { get; set; } = twin;

        public bool FailReads { get; set; }

        public Task ResetAsync(string correlationId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ApplyScenarioAsync(EnergyScenarioSyncRequest request, string correlationId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<EnergyOperationalTwin> GetStateAsync(string assetId, string correlationId, CancellationToken cancellationToken) =>
            FailReads
                ? throw new HttpRequestException("Energy Hub unavailable.")
                : Task.FromResult(Twin);
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

    private sealed class FakeScenarios(ScenarioId current, List<string> log, Action<ScenarioId> onApplied) : IScenarioApplier
    {
        public ScenarioId Current { get; private set; } = current;

        public ScenarioApplicationStatus ApplicationStatus { get; set; } = ScenarioApplicationStatus.Applied;

        public ScenarioStatus GetCurrentScenario() =>
            new(Current, Current.ToString(), "Test scenario", DateTimeOffset.UtcNow, "scenario-corr", ApplicationStatus);

        public Task<ScenarioApplicationResult> ApplyAsync(ScenarioId scenarioId, string correlationId, CancellationToken cancellationToken)
        {
            Current = scenarioId;
            ApplicationStatus = ScenarioApplicationStatus.Applied;
            log.Add($"scenario:{scenarioId}");
            onApplied(scenarioId);
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
