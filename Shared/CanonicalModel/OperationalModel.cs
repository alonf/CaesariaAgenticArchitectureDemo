namespace Caesarea.CanonicalModel;

/// <summary>
/// Defines the canonical Stage 0 demo assets that remain stable across deterministic services and web applications.
/// </summary>
public static class DemoAssets
{
    /// <summary>
    /// Gets the canonical streetlight asset identifier used throughout the deterministic demo.
    /// </summary>
    public const string StreetlightAssetId = "L-417";

    /// <summary>
    /// Gets the canonical operational area used throughout the deterministic demo.
    /// </summary>
    public const string NorthPromenadeArea = "North Promenade";
}

/// <summary>
/// Represents the health state of the physical streetlight controller.
/// </summary>
public enum ControllerHealthStatus
{
    /// <summary>
    /// The controller is healthy and can acknowledge commands.
    /// </summary>
    Healthy,

    /// <summary>
    /// The controller is faulted and cannot complete requested commands.
    /// </summary>
    Faulted
}

/// <summary>
/// Represents the execution state of a deterministic device command.
/// </summary>
public enum CommandExecutionStatus
{
    /// <summary>
    /// The command has been accepted but not yet confirmed by the downstream system.
    /// </summary>
    Pending,

    /// <summary>
    /// The command completed successfully and the downstream system confirmed the outcome.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The command failed and the authoritative reported state did not change.
    /// </summary>
    Failed,

    /// <summary>
    /// The command did not receive a downstream acknowledgement before the timeout window closed.
    /// </summary>
    TimedOut
}

/// <summary>
/// Identifies the system boundary that emitted an operational activity record.
/// </summary>
public enum ActivitySource
{
    /// <summary>
    /// The vendor-facing SmartPole simulator emitted the activity.
    /// </summary>
    SmartPoleSimulator,

    /// <summary>
    /// The Energy Hub emitted the activity.
    /// </summary>
    EnergyHub,

    /// <summary>
    /// The Command Center emitted the activity.
    /// </summary>
    CommandCenter,

    /// <summary>
    /// The Demo Scenario coordinator emitted the activity.
    /// </summary>
    DemoScenario
}

/// <summary>
/// Categorizes the purpose of an operational activity record.
/// </summary>
public enum ActivityKind
{
    /// <summary>
    /// The activity describes deterministic scenario orchestration.
    /// </summary>
    Scenario,

    /// <summary>
    /// The activity describes an operator or service command.
    /// </summary>
    Command,

    /// <summary>
    /// The activity describes boundary synchronization between deterministic services.
    /// </summary>
    Synchronization,

    /// <summary>
    /// The activity describes an observed or projected operational state.
    /// </summary>
    Observation,

    /// <summary>
    /// The activity describes incident lifecycle or context changes.
    /// </summary>
    Incident
}

/// <summary>
/// Describes controller health using the canonical operational language shared across service boundaries.
/// </summary>
/// <param name="Status">The controller health status value.</param>
/// <param name="Summary">A projector-friendly description of the controller state.</param>
public sealed record ControllerHealthInfo(ControllerHealthStatus Status, string Summary)
{
    /// <summary>
    /// Gets the canonical healthy controller description.
    /// </summary>
    public static ControllerHealthInfo Healthy { get; } = new(ControllerHealthStatus.Healthy, "Controller healthy");

    /// <summary>
    /// Gets the canonical faulted controller description.
    /// </summary>
    public static ControllerHealthInfo Faulted { get; } = new(ControllerHealthStatus.Faulted, "Controller faulted");
}

/// <summary>
/// Captures cross-domain operational context that can affect the scheduled lighting target.
/// </summary>
/// <param name="SecurityOperationActive">Indicates whether a security operation is active in the area.</param>
/// <param name="RequiresLighting">Indicates whether the current operational context requires lighting regardless of schedule.</param>
/// <param name="Summary">A short description of the contextual lighting requirement.</param>
public sealed record OperationalContext(bool SecurityOperationActive, bool RequiresLighting, string Summary)
{
    /// <summary>
    /// Gets the canonical context indicating that no cross-domain requirement currently affects the schedule.
    /// </summary>
    public static OperationalContext None { get; } = new(false, false, "No cross-domain lighting requirement.");
}

/// <summary>
/// Represents a correlated command request and its completion state.
/// </summary>
/// <param name="Operation">The projector-friendly operation name.</param>
/// <param name="DesiredIsOn">The desired lamp state requested by the operation.</param>
/// <param name="Status">The downstream execution status.</param>
/// <param name="CorrelationId">The correlation identifier spanning the end-to-end request.</param>
/// <param name="RequestedAt">The time at which the command was requested.</param>
/// <param name="CompletedAt">The time at which the command completed, if known.</param>
/// <param name="Summary">The final or interim execution summary.</param>
public sealed record CommandRecord(
    string Operation,
    bool DesiredIsOn,
    CommandExecutionStatus Status,
    string CorrelationId,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt,
    string Summary);

/// <summary>
/// Represents a single correlated activity emitted by one deterministic service boundary.
/// </summary>
/// <param name="Id">The activity identifier.</param>
/// <param name="AssetId">The related asset identifier.</param>
/// <param name="CorrelationId">The correlation identifier spanning the end-to-end request.</param>
/// <param name="Source">The emitting service or adapter boundary.</param>
/// <param name="Kind">The activity category.</param>
/// <param name="Message">The activity message shown in the UI timeline.</param>
/// <param name="OccurredAt">The time at which the activity occurred.</param>
/// <param name="IsSuccess">Indicates whether the activity represents a successful outcome.</param>
/// <param name="CommandStatus">The related command status when the activity describes a command.</param>
public sealed record ActivityRecord(
    string Id,
    string AssetId,
    string CorrelationId,
    ActivitySource Source,
    ActivityKind Kind,
    string Message,
    DateTimeOffset OccurredAt,
    bool IsSuccess,
    CommandExecutionStatus? CommandStatus);
