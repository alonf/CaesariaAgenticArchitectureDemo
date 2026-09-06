using Polly.Timeout;

namespace DemoScenario.Api.Services;

/// <summary>
/// The part of the scenario coordinator the director drives: what is applied now, and applying one.
/// </summary>
public interface IScenarioApplier
{
    /// <summary>Gets the scenario applied now.</summary>
    public ScenarioStatus GetCurrentScenario();

    /// <summary>Applies a scenario across the deterministic services.</summary>
    public Task<ScenarioApplicationResult> ApplyAsync(ScenarioId scenarioId, string correlationId, CancellationToken cancellationToken);
}

/// <summary>
/// The part of the stage coordinator the director drives: the authoritative stage, and setting it.
/// </summary>
public interface IStageApplier
{
    /// <summary>Gets the authoritative current stage.</summary>
    public Task<DemoStageStatus> GetCurrentStageAsync(string correlationId, CancellationToken cancellationToken);

    /// <summary>Sets the stage and propagates it.</summary>
    public Task<DemoStageChangeResult> ApplyAsync(DemoStage stage, string correlationId, CancellationToken cancellationToken);
}

/// <summary>
/// Turns a beat's prerequisites into a readiness check and, on request, into the actions that meet
/// them. A beat is prepared in a fixed order - the scenario fixture, then the stage, then the
/// switches - because the agent gates its switches by stage and a stage change resets them: set a
/// switch first and the stage change would undo it. Preparation touches only what the beat starts
/// from; a prerequisite belonging to a later step is reported, never applied, so the beat's own
/// flip is left for the presenter to perform on stage.
/// </summary>
public sealed partial class DemoDirector(
    StageCatalog stageCatalog,
    ScenarioCatalog scenarioCatalog,
    IStageApplier stages,
    IScenarioApplier scenarios,
    IOperationsAgentSwitchClient switches,
    IEnergyScenarioClient energy,
    ILogger<DemoDirector> logger)
{
    private readonly StageCatalog _stageCatalog = stageCatalog ?? throw new ArgumentNullException(nameof(stageCatalog));
    private readonly ScenarioCatalog _scenarioCatalog = scenarioCatalog ?? throw new ArgumentNullException(nameof(scenarioCatalog));
    private readonly IStageApplier _stages = stages ?? throw new ArgumentNullException(nameof(stages));
    private readonly IScenarioApplier _scenarios = scenarios ?? throw new ArgumentNullException(nameof(scenarios));
    private readonly IOperationsAgentSwitchClient _switches = switches ?? throw new ArgumentNullException(nameof(switches));
    private readonly IEnergyScenarioClient _energy = energy ?? throw new ArgumentNullException(nameof(energy));
    private readonly ILogger<DemoDirector> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Checks a beat's prerequisites against the live demo.
    /// </summary>
    public async Task<DemoStageReadiness> GetReadinessAsync(DemoStage stage, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        var descriptor = _stageCatalog.GetDescriptor(stage);
        var current = await _stages.GetCurrentStageAsync(correlationId, cancellationToken);
        var scenario = _scenarios.GetCurrentScenario();
        List<DemoPrerequisiteStatus> statuses = [];

        foreach (var prerequisite in descriptor.Walkthrough?.Prerequisites ?? [])
        {
            statuses.Add(prerequisite.Kind switch
            {
                DemoPrerequisiteKind.Scenario => await CheckScenarioAsync(prerequisite, scenario, correlationId, cancellationToken),
                DemoPrerequisiteKind.Switch => await ReadSwitchAsync(prerequisite, correlationId, cancellationToken),
                _ => new DemoPrerequisiteStatus(prerequisite, null, null)
            });
        }

        return new DemoStageReadiness(stage, descriptor.Name, current.Id, statuses);
    }

    /// <summary>
    /// Meets a beat's starting prerequisites, in the order that makes them stick, and reports what
    /// was done and where the beat stands afterwards.
    /// </summary>
    public async Task<DemoStagePrepareResult> PrepareAsync(DemoStage stage, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        var descriptor = _stageCatalog.GetDescriptor(stage);
        var before = await GetReadinessAsync(stage, correlationId, cancellationToken);
        List<string> actions = [];

        DemoDirectorLog.Preparing(_logger, descriptor.Name, correlationId);

        // 1. The fixture. A scenario application also resets the simulator, so it goes first. A
        //    fixture the city has drifted from - the operator restored the lamp during the previous
        //    beat - reads as unmet and is applied again, exactly like a different one.
        foreach (var status in before.Prerequisites.Where(static status =>
                     status.Prerequisite is { Kind: DemoPrerequisiteKind.Scenario, AppliesAtStart: true } && status.Satisfied == false))
        {
            var result = await _scenarios.ApplyAsync(status.Prerequisite.ScenarioId!.Value, correlationId, cancellationToken);
            actions.Add(result.Summary);
        }

        // 2. The stage. A downgrade resets the agent's switches, so they are set only after this.
        if (!before.StageIsCurrent)
        {
            var result = await _stages.ApplyAsync(stage, correlationId, cancellationToken);
            actions.Add(result.Summary);
        }

        // 3. The switches the beat starts from. An unreadable switch is set as well - the value it
        //    needs is known even when the value it has is not.
        var switchesToSet = before.Prerequisites
            .Where(static status => status.Prerequisite is { Kind: DemoPrerequisiteKind.Switch, AppliesAtStart: true } && status.Satisfied != true)
            .Select(static status => status.Prerequisite);

        foreach (var prerequisite in switchesToSet)
        {
            var name = DemoSwitchValues.Describe(prerequisite.Switch!.Value);

            try
            {
                await _switches.SetAsync(prerequisite.Switch.Value, prerequisite.RequiredValue!, correlationId, cancellationToken);
                actions.Add($"{name} set to {prerequisite.RequiredValue}.");
            }
            catch (Exception exception) when (IsServiceFailure(exception, cancellationToken))
            {
                DemoDirectorLog.SwitchNotSet(_logger, prerequisite.Switch.Value, correlationId, exception);
                actions.Add($"{name} could not be set to {prerequisite.RequiredValue}: {exception.Message}");
            }
        }

        var after = await GetReadinessAsync(stage, correlationId, cancellationToken);
        var summary = Summarize(descriptor.Name, after);

        DemoDirectorLog.Prepared(_logger, descriptor.Name, actions.Count, after.Ready, correlationId);
        return new DemoStagePrepareResult(after, actions, summary);
    }

    // Unknown is said out loud: a beat whose only open question is a check that could not run is
    // not "ready", and it is not "unmet" either.
    private static string Summarize(string stageName, DemoStageReadiness after)
    {
        if (after.Ready)
        {
            return $"{stageName} is ready to present.";
        }

        var unmet = after.Prerequisites.Any(static status => status.Prerequisite.AppliesAtStart && status.Satisfied == false);

        if (!unmet && after.StageIsCurrent && after.Unverified.Count > 0)
        {
            var names = string.Join(" and ", after.Unverified.Select(static status => status.Prerequisite.Text));
            return $"{stageName} is set, but this could not be verified: {names}.";
        }

        return $"{stageName} is set, but a prerequisite is still unmet - check the list.";
    }

    // The scenario's identifier is not the city's state: the operator restores L-417 during the
    // deterministic beat and the scenario still reads Lights On, and an application that failed
    // halfway keeps the identifier it asked for. So the fixture is checked three ways - the
    // identifier, the application's outcome, and the asset as the Energy Hub reports it now.
    private async Task<DemoPrerequisiteStatus> CheckScenarioAsync(
        DemoPrerequisite prerequisite, ScenarioStatus scenario, string correlationId, CancellationToken cancellationToken)
    {
        var required = prerequisite.ScenarioId!.Value;

        if (scenario.Id != required)
        {
            return new DemoPrerequisiteStatus(prerequisite, false, scenario.Name);
        }

        if (scenario.ApplicationStatus != ScenarioApplicationStatus.Applied)
        {
            return new DemoPrerequisiteStatus(prerequisite, false, $"{scenario.Name} ({scenario.ApplicationStatus})");
        }

        var recipe = _scenarioCatalog.GetRecipe(required);

        try
        {
            var twin = await _energy.GetStateAsync(DemoAssets.StreetlightAssetId, correlationId, cancellationToken);
            var drift = DescribeDrift(recipe, twin);

            return drift is null
                ? new DemoPrerequisiteStatus(prerequisite, true, scenario.Name)
                : new DemoPrerequisiteStatus(prerequisite, false, $"{scenario.Name}, but {drift}");
        }
        catch (Exception exception) when (IsServiceFailure(exception, cancellationToken))
        {
            DemoDirectorLog.FixtureUnreadable(_logger, required, correlationId, exception);
            return new DemoPrerequisiteStatus(prerequisite, null, scenario.Name);
        }
    }

    // What a beat's first step relies on: the lamp, the override, the controller, the incident.
    // Anything else the scenario sets - dates, notes, the customer report - does not change what
    // the agent or the operator sees on the first click.
    private static string? DescribeDrift(ScenarioRecipe recipe, EnergyOperationalTwin twin)
    {
        var expected = recipe.SmartPoleState;

        if (twin.ReportedIsOn != expected.IsOn)
        {
            return $"{twin.AssetId} is now {(twin.ReportedIsOn ? "on" : "off")}";
        }

        if (twin.ManualOverride != expected.ManualOverride)
        {
            return twin.ManualOverride ? "a manual override is now active" : "the manual override is gone";
        }

        if (twin.ControllerHealth.Status != expected.ControllerHealth.Status)
        {
            return $"the controller is now {twin.ControllerHealth.Status}";
        }

        if (!string.Equals(twin.OpenIncidentId, recipe.EnergyState.OpenIncidentId, StringComparison.Ordinal))
        {
            return twin.OpenIncidentId is null ? "the incident is gone" : $"incident {twin.OpenIncidentId} is open";
        }

        return null;
    }

    private async Task<DemoPrerequisiteStatus> ReadSwitchAsync(DemoPrerequisite prerequisite, string correlationId, CancellationToken cancellationToken)
    {
        try
        {
            var value = await _switches.GetAsync(prerequisite.Switch!.Value, correlationId, cancellationToken);
            var satisfied = string.Equals(value, prerequisite.RequiredValue, StringComparison.OrdinalIgnoreCase);
            return new DemoPrerequisiteStatus(prerequisite, satisfied, value);
        }
        catch (Exception exception) when (IsServiceFailure(exception, cancellationToken))
        {
            // Unknown, and reported as such: the presenter sees a question mark, the beat does not
            // read as ready, and preparation still sets the switch.
            DemoDirectorLog.SwitchUnreadable(_logger, prerequisite.Switch!.Value, correlationId, exception);
            return new DemoPrerequisiteStatus(prerequisite, null, null);
        }
    }

    // The resilience pipeline surfaces its attempt timeout as TimeoutRejectedException and its own
    // cancellation as OperationCanceledException with the caller's token untouched; both are the
    // service being unavailable. Real caller cancellation propagates.
    private static bool IsServiceFailure(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException or TimeoutRejectedException
        || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested);
}

internal static partial class DemoDirectorLog
{
    [LoggerMessage(
        EventId = 2150,
        Level = LogLevel.Information,
        Message = "Preparing the {StageName} beat. CorrelationId: {CorrelationId}.")]
    internal static partial void Preparing(ILogger logger, string stageName, string correlationId);

    [LoggerMessage(
        EventId = 2151,
        Level = LogLevel.Information,
        Message = "The {StageName} beat was prepared with {ActionCount} action(s); ready: {Ready}. CorrelationId: {CorrelationId}.")]
    internal static partial void Prepared(ILogger logger, string stageName, int actionCount, bool ready, string correlationId);

    [LoggerMessage(
        EventId = 2152,
        Level = LogLevel.Warning,
        Message = "The {Switch} switch could not be read. CorrelationId: {CorrelationId}.")]
    internal static partial void SwitchUnreadable(ILogger logger, DemoSwitch @switch, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 2153,
        Level = LogLevel.Warning,
        Message = "The {Switch} switch could not be set. CorrelationId: {CorrelationId}.")]
    internal static partial void SwitchNotSet(ILogger logger, DemoSwitch @switch, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 2154,
        Level = LogLevel.Warning,
        Message = "The {ScenarioId} fixture could not be checked against the Energy Hub. CorrelationId: {CorrelationId}.")]
    internal static partial void FixtureUnreadable(ILogger logger, ScenarioId scenarioId, string correlationId, Exception exception);
}
