namespace OperationsAgent.Hosted;

internal static partial class HostedAgentLog
{
    [LoggerMessage(
        EventId = 2700,
        Level = LogLevel.Warning,
        Message = "No skills directory at {Path}; the agent runs without documented procedures.")]
    internal static partial void SkillsDirectoryMissing(ILogger logger, string path);

    [LoggerMessage(
        EventId = 2701,
        Level = LogLevel.Information,
        Message = "Hosting environment: platform injected PORT={Port}. Variables supplied by the platform: {VariableNames}.")]
    internal static partial void HostingEnvironment(ILogger logger, string port, string variableNames);

    [LoggerMessage(
        EventId = 2702,
        Level = LogLevel.Information,
        Message = "Sandbox TCP listeners already bound before this host started: {Listeners}.")]
    internal static partial void ActiveListeners(ILogger logger, string listeners);

    [LoggerMessage(
        EventId = 2703,
        Level = LogLevel.Error,
        Message = "The agent host failed to start. TCP listeners at the moment of failure: {Listeners}.")]
    internal static partial void StartupFailed(ILogger logger, string listeners, Exception exception);
}
