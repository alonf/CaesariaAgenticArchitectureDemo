namespace Caesarea.Contracts;

public static class DemoAssets
{
    public const string StreetlightAssetId = "L-417";
    public const string NorthPromenadeArea = "North Promenade";
}

public static class DemoStageNames
{
    public const string Deterministic = "Deterministic";
}

public static class CorrelationHeaderNames
{
    public const string XCorrelationId = "X-Correlation-ID";
}

public static class CorrelationIds
{
    public static string Create() => Guid.NewGuid().ToString("N");
}

public enum ScenarioId
{
    ForgottenOverride,
    SecurityOperation,
    ControllerFault,
    ExistingIncident,
    NormalOperation,
    NightOperation
}

public enum ControllerHealthStatus
{
    Healthy,
    Faulted
}

public enum CommandExecutionStatus
{
    Pending,
    Succeeded,
    Failed,
    TimedOut
}

public enum ActivitySource
{
    SmartPoleSimulator,
    EnergyHub,
    CommandCenter,
    DemoScenario
}

public enum ActivityKind
{
    Scenario,
    Command,
    Synchronization,
    Observation,
    Incident
}

public enum IncidentSeverity
{
    Advisory,
    Warning,
    Critical
}

public enum IncidentStatus
{
    Open,
    Resolved
}
