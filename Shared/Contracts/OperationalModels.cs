namespace Caesarea.Contracts;

public sealed record ControllerHealthInfo(ControllerHealthStatus Status, string Summary)
{
    public static ControllerHealthInfo Healthy { get; } = new(ControllerHealthStatus.Healthy, "Controller healthy");

    public static ControllerHealthInfo Faulted { get; } = new(ControllerHealthStatus.Faulted, "Controller faulted");
}

public sealed record OperationalContext(bool SecurityOperationActive, bool RequiresLighting, string Summary)
{
    public static OperationalContext None { get; } = new(false, false, "No cross-domain lighting requirement.");
}

public sealed record CommandRecord(
    string Operation,
    bool DesiredIsOn,
    CommandExecutionStatus Status,
    string CorrelationId,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt,
    string Summary);

public sealed record SmartPoleBehaviorConfiguration(int CommandDelayMs, bool SimulateTimeout, bool SimulateFailure)
{
    public static SmartPoleBehaviorConfiguration Default { get; } = new(0, false, false);
}

public sealed record SmartPoleScenarioState(
    bool IsOn,
    bool IsDaylight,
    bool ExpectedScheduledState,
    bool ManualOverride,
    ControllerHealthInfo ControllerHealth,
    DateTimeOffset? LastMaintenanceTime,
    bool HasRecentMaintenance,
    OperationalContext OperationContext,
    SmartPoleBehaviorConfiguration Configuration);

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

public sealed record SetLampStateCommand(string AssetId, bool DesiredIsOn);

public sealed record SmartPoleCommandResult(
    string AssetId,
    bool? ActualIsOn,
    CommandExecutionStatus Status,
    string CorrelationId,
    string Summary,
    DateTimeOffset CompletedAt);

public sealed record EnergyScenarioSyncRequest(bool DesiredIsOn, string? OpenIncidentId, string Summary);

public sealed record EnergyOperationalTwin(
    string AssetId,
    string Area,
    bool ReportedIsOn,
    bool DesiredIsOn,
    bool IsDaylight,
    bool ExpectedScheduledState,
    bool ManualOverride,
    ControllerHealthInfo ControllerHealth,
    CommandRecord? LastCommand,
    DateTimeOffset? LastMaintenanceTime,
    bool HasRecentMaintenance,
    string? OpenIncidentId,
    DateTimeOffset LastReportedAt,
    OperationalContext OperationContext);

public sealed record RestoreScheduledModeResult(
    string AssetId,
    bool DesiredIsOn,
    bool ReportedIsOn,
    CommandExecutionStatus Status,
    string CorrelationId,
    string Summary,
    DateTimeOffset CompletedAt);

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
