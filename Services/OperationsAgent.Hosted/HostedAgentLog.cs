namespace OperationsAgent.Hosted;

internal static partial class HostedAgentLog
{
    [LoggerMessage(
        EventId = 2700,
        Level = LogLevel.Warning,
        Message = "No skills directory at {Path}; the agent runs without documented procedures.")]
    internal static partial void SkillsDirectoryMissing(ILogger logger, string path);
}
