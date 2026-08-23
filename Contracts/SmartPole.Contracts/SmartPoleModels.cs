using System.ComponentModel.DataAnnotations;
using Caesarea.CanonicalModel;

namespace SmartPole.Contracts;

/// <summary>
/// Configures deterministic simulator timing and fault behavior for the SmartPole boundary.
/// </summary>
/// <param name="CommandDelayMs">The deterministic delay applied before the simulator completes a command.</param>
/// <param name="SimulateTimeout">Indicates whether the simulator should time out commands.</param>
/// <param name="SimulateFailure">Indicates whether the simulator should reject commands.</param>
public sealed record SmartPoleBehaviorConfiguration(
    [property: Range(0, 30000)] int CommandDelayMs,
    bool SimulateTimeout,
    bool SimulateFailure)
{
    /// <summary>
    /// Gets the default simulator behavior that completes immediately without injecting failures.
    /// </summary>
    public static SmartPoleBehaviorConfiguration Default { get; } = new(0, false, false);
}

/// <summary>
/// Represents the simulator state used to seed a deterministic scenario.
/// </summary>
/// <param name="IsOn">Indicates whether the lamp is physically on.</param>
/// <param name="IsDaylight">Indicates whether the asset is currently in daylight.</param>
/// <param name="ExpectedScheduledState">Indicates the schedule-driven state expected by normal deterministic operation.</param>
/// <param name="ManualOverride">Indicates whether a manual override is active.</param>
/// <param name="ControllerHealth">The controller health projected into the simulator.</param>
/// <param name="LastMaintenanceTime">The last maintenance time to show in the operational narrative.</param>
/// <param name="HasRecentMaintenance">Indicates whether recent maintenance should be shown in the UI.</param>
/// <param name="OperationContext">The contextual requirement that can override the normal schedule.</param>
/// <param name="Configuration">The simulator behavior configuration to apply with the scenario.</param>
public sealed record SmartPoleScenarioState(
    bool IsOn,
    bool IsDaylight,
    bool ExpectedScheduledState,
    bool ManualOverride,
    [property: Required] ControllerHealthInfo ControllerHealth,
    DateTimeOffset? LastMaintenanceTime,
    bool HasRecentMaintenance,
    [property: Required] OperationalContext OperationContext,
    [property: Required] SmartPoleBehaviorConfiguration Configuration);

/// <summary>
/// Represents the authoritative physical state exposed by the SmartPole simulator boundary.
/// </summary>
/// <param name="AssetId">The asset identifier.</param>
/// <param name="Area">The area containing the asset.</param>
/// <param name="IsOn">Indicates whether the physical lamp is on.</param>
/// <param name="IsDaylight">Indicates whether the physical asset is currently in daylight.</param>
/// <param name="ExpectedScheduledState">Indicates the current schedule-driven target.</param>
/// <param name="ManualOverride">Indicates whether a manual override is active.</param>
/// <param name="ControllerHealth">The controller health reported by the simulator.</param>
/// <param name="LastCommand">The last device command observed by the simulator.</param>
/// <param name="LastMaintenanceTime">The most recent maintenance timestamp.</param>
/// <param name="HasRecentMaintenance">Indicates whether the UI should show recent maintenance context.</param>
/// <param name="OperationContext">The contextual requirement that can override the normal schedule.</param>
/// <param name="LastReportedAt">The timestamp of the last authoritative device report.</param>
/// <param name="Configuration">The deterministic simulator behavior configuration.</param>
public sealed record SmartPolePhysicalState(
    string AssetId,
    string Area,
    bool IsOn,
    bool IsDaylight,
    bool ExpectedScheduledState,
    bool ManualOverride,
    ControllerHealthInfo ControllerHealth,
    CommandRecord? LastCommand,
    DateTimeOffset? LastMaintenanceTime,
    bool HasRecentMaintenance,
    OperationalContext OperationContext,
    DateTimeOffset LastReportedAt,
    SmartPoleBehaviorConfiguration Configuration);

/// <summary>
/// Represents a vendor-facing request to change the lamp state.
/// </summary>
/// <param name="AssetId">The asset identifier targeted by the command.</param>
/// <param name="DesiredIsOn">The desired lamp state.</param>
public sealed record SetLampStateCommand(
    [property: Required]
    [property: StringLength(64, MinimumLength = 1)] string AssetId,
    [property: Required] bool? DesiredIsOn);

/// <summary>
/// Represents the deterministic outcome of a SmartPole command request.
/// </summary>
/// <param name="AssetId">The asset identifier targeted by the command.</param>
/// <param name="ActualIsOn">The confirmed physical lamp state when acknowledgement succeeds.</param>
/// <param name="Status">The downstream execution status.</param>
/// <param name="CorrelationId">The correlation identifier spanning the end-to-end request.</param>
/// <param name="Summary">The projector-friendly completion summary.</param>
/// <param name="CompletedAt">The time at which the command completed.</param>
public sealed record SmartPoleCommandResult(
    string AssetId,
    bool? ActualIsOn,
    CommandExecutionStatus Status,
    string CorrelationId,
    string Summary,
    DateTimeOffset CompletedAt);
