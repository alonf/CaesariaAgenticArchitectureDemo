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
    IStageApplier stages,
    IScenarioApplier scenarios,
    IOperationsAgentSwitchClient switches,
    ILogger<DemoDirector> logger)
{
    private readonly StageCatalog _stageCatalog = stageCatalog ?? throw new ArgumentNullException(nameof(stageCatalog));
    private readonly IStageApplier _stages = stages ?? throw new ArgumentNullException(nameof(stages));
    private readonly IScenarioApplier _scenarios = scenarios ?? throw new ArgumentNullException(nameof(scenarios));
    private readonly IOperationsAgentSwitchClient _switches = switches ?? throw new ArgumentNullException(nameof(switches));
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
                DemoPrerequisiteKind.Scenario => new DemoPrerequisiteStatus(prerequisite, scenario.Id == prerequisite.ScenarioId, scenario.Name),
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

        // 1. The fixture. A scenario application also resets the simulator, so it goes first.
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
        var summary = after.Ready
            ? $"{descriptor.Name} is ready to present."
            : $"{descriptor.Name} is set, but a prerequisite is still unmet - check the list.";

        DemoDirectorLog.Prepared(_logger, descriptor.Name, actions.Count, after.Ready, correlationId);
        return new DemoStagePrepareResult(after, actions, summary);
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
            // Unknown is not unmet: the presenter sees a question mark, not a blocker, and the
            // switch is still set by preparation.
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
}
