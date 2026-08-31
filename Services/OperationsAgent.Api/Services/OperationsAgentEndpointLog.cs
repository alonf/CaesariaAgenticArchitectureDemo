namespace OperationsAgent.Api.Services;

/// <summary>
/// Source-generated log messages for the Operations Agent ask endpoint.
/// </summary>
internal static partial class OperationsAgentEndpointLog
{
    [LoggerMessage(
        EventId = 2450,
        Level = LogLevel.Warning,
        Message = "Operations Agent could not authenticate to Microsoft Foundry. CorrelationId: {CorrelationId}.")]
    internal static partial void AuthenticationFailed(ILogger logger, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 2610,
        Level = LogLevel.Warning,
        Message = "Skills directory '{SkillsDirectory}' could not be located; the Skills stage will run without skills.")]
    internal static partial void SkillsDirectoryMissing(ILogger logger, string skillsDirectory);

    [LoggerMessage(
        EventId = 2451,
        Level = LogLevel.Warning,
        Message = "Operations Agent request exceeded its execution budget. CorrelationId: {CorrelationId}.")]
    internal static partial void ExecutionTimedOut(ILogger logger, string correlationId, Exception exception);
}
