namespace DemoControl.Web.Services;

/// <summary>
/// Debugger operations, logged apart from domain events so a stage-side problem - the wrong IDE,
/// a helper that is not built, a process that vanished - is found by one filter on the category.
/// </summary>
internal static partial class DebuggerIntegrationLog
{
    [LoggerMessage(
        EventId = 2610,
        Level = LogLevel.Debug,
        Message = "[DebuggerIntegration] IDE={Ide} Operation=Availability Available={Available} Summary={Summary}")]
    internal static partial void AvailabilityChecked(ILogger logger, string ide, bool available, string summary);

    [LoggerMessage(
        EventId = 2611,
        Level = LogLevel.Information,
        Message = "[DebuggerIntegration] IDE={Ide} Operation=Setup Result={Result}")]
    internal static partial void SetupCompleted(ILogger logger, string ide, string result);

    [LoggerMessage(
        EventId = 2612,
        Level = LogLevel.Information,
        Message = "[DebuggerIntegration] IDE={Ide} Operation={Operation} Process={Process} PID={ProcessId} Result={Result}")]
    internal static partial void DebuggerRequested(ILogger logger, string ide, string operation, string process, int? processId, string result);

    [LoggerMessage(
        EventId = 2613,
        Level = LogLevel.Warning,
        Message = "[DebuggerIntegration] IDE={Ide} Operation={Operation} Process={Process} PID={ProcessId} Result=Failed Reason={Reason}")]
    internal static partial void DebuggerRequestFailed(ILogger logger, string ide, string operation, string process, int? processId, string reason);
}
