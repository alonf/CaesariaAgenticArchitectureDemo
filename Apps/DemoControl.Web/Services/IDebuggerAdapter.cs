namespace DemoControl.Web.Services;

/// <summary>
/// One IDE the switchboard can ask to attach its .NET debugger to a demo service, or to let go of
/// it again. The services never learn which IDE is on them: they see <c>Debugger.IsAttached</c>
/// and nothing else, so every adapter lives here, on the presenter's side of that line.
/// </summary>
internal interface IDebuggerAdapter
{
    /// <summary>
    /// Gets the stable identifier the presenter's choice is remembered by, for example <c>vscode</c>.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the projector-friendly name, for example <c>Visual Studio 2026</c>.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Reports whether this IDE can attach on this machine right now, and what stands in the way
    /// when it cannot. Never throws for an absent IDE: absence is an ordinary answer.
    /// </summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The availability and a short presenter-facing summary.</returns>
    public Task<DebuggerAvailability> GetAvailabilityAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Performs the one-time setup the availability check asked for - installing an extension,
    /// building a helper - so the presenter fixes a missing prerequisite from the switchboard.
    /// </summary>
    /// <param name="cancellationToken">Cancels the setup.</param>
    /// <returns>The outcome with a presenter-facing message.</returns>
    public Task<DebuggerCommandResult> SetUpAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Asks the IDE to attach its .NET debugger to one running demo service.
    /// </summary>
    /// <param name="target">The service process to attach to.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The outcome with a presenter-facing message.</returns>
    public Task<DebuggerCommandResult> AttachAsync(DebuggerTarget target, CancellationToken cancellationToken);

    /// <summary>
    /// Asks the IDE to detach its debugger from one demo service. The service keeps running; only
    /// the debugger lets go.
    /// </summary>
    /// <param name="target">The service process to detach from.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The outcome with a presenter-facing message.</returns>
    public Task<DebuggerCommandResult> DetachAsync(DebuggerTarget target, CancellationToken cancellationToken);

    /// <summary>
    /// Reports whether this IDE's debugger is on a process right now, for an attachment the
    /// switchboard did not make itself - a launch.json attach, or one from before a restart.
    /// </summary>
    /// <param name="target">The service process.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns><see langword="true"/> or <see langword="false"/> when the IDE can tell; <see langword="null"/> when it cannot.</returns>
    public Task<bool?> IsAttachedAsync(DebuggerTarget target, CancellationToken cancellationToken);
}

/// <summary>
/// The service process a debugger is pointed at. The id, when the service reported one, is the
/// exact answer; the name is the fallback for a service that predates the id and the label a
/// debug session is named after.
/// </summary>
/// <param name="ProcessName">The process name on this platform, for example OperationsAgent.Api.exe on Windows.</param>
/// <param name="ProcessId">The process id the service reported, or <see langword="null"/> when it did not.</param>
internal sealed record DebuggerTarget(string ProcessName, int? ProcessId);

/// <summary>
/// Whether an IDE can attach on this machine, and what to do about it when it cannot.
/// </summary>
/// <param name="Available">Whether attach and detach can be requested now.</param>
/// <param name="Summary">A short stamp for the picker, for example <c>ready</c> or <c>not installed</c>.</param>
/// <param name="Detail">A presenter-facing sentence explaining an unavailable IDE, or a note about an available one.</param>
/// <param name="SetupAction">The label of the setup the adapter can perform to become available, or <see langword="null"/> when there is none.</param>
internal sealed record DebuggerAvailability(bool Available, string Summary, string? Detail = null, string? SetupAction = null);

/// <summary>
/// Reports the outcome of one debugger request.
/// </summary>
/// <param name="Succeeded">Whether the request succeeded.</param>
/// <param name="Message">A presenter-facing outcome message.</param>
internal sealed record DebuggerCommandResult(bool Succeeded, string Message);

/// <summary>
/// Names demo service processes the way the running platform does: Windows appends <c>.exe</c>
/// to the apphost, macOS and Linux do not.
/// </summary>
internal static class DemoProcessNames
{
    /// <summary>
    /// Gets the process name of a service on the platform this switchboard runs on.
    /// </summary>
    /// <param name="serviceName">The service's assembly name, for example OperationsAgent.Api.</param>
    /// <returns>The process name a debugger sees.</returns>
    public static string OnThisPlatform(string serviceName) => For(serviceName, OperatingSystem.IsWindows());

    /// <summary>
    /// Gets the process name of a service on the given platform.
    /// </summary>
    /// <param name="serviceName">The service's assembly name, for example OperationsAgent.Api.</param>
    /// <param name="isWindows">Whether the platform is Windows.</param>
    /// <returns>The process name a debugger sees.</returns>
    public static string For(string serviceName, bool isWindows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        return isWindows ? $"{serviceName}.exe" : serviceName;
    }
}
