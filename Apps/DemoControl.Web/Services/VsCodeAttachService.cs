using System.Diagnostics;

namespace DemoControl.Web.Services;

/// <summary>
/// Drives the presenter-machine VS Code instance for the demo-breakpoint feature: detects the
/// companion demo-attach extension, installs it from the repository .vsix, and asks VS Code to
/// attach the .NET debugger to the Operations Agent service.
/// </summary>
internal sealed partial class VsCodeAttachService(IWebHostEnvironment environment, ILogger<VsCodeAttachService> logger)
{
    internal const string ExtensionId = "caesarea-demo.demo-attach";
    internal const string OperationsAgentProcessName = "OperationsAgent.Api.exe";
    private const string VsixRelativePath = "../../tools/vscode-demo-attach/demo-attach-0.1.0.vsix";
    private static readonly TimeSpan CliTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets whether the VS Code CLI is reachable and whether the demo-attach extension is installed.
    /// </summary>
    /// <param name="cancellationToken">Cancels the CLI call.</param>
    /// <returns>The current attach tooling status.</returns>
    public async Task<VsCodeAttachStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        var result = await RunCodeCliAsync("--list-extensions", cancellationToken);

        if (!result.Succeeded)
        {
            VsCodeAttachLog.StatusChecked(logger, vsCodeAvailable: false, extensionInstalled: false);
            return new VsCodeAttachStatus(VsCodeAvailable: false, ExtensionInstalled: false);
        }

        var installed = result.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(extension => string.Equals(extension, ExtensionId, StringComparison.OrdinalIgnoreCase));

        VsCodeAttachLog.StatusChecked(logger, vsCodeAvailable: true, extensionInstalled: installed);
        return new VsCodeAttachStatus(VsCodeAvailable: true, ExtensionInstalled: installed);
    }

    /// <summary>
    /// Installs the demo-attach extension from the repository .vsix package.
    /// </summary>
    /// <param name="cancellationToken">Cancels the CLI call.</param>
    /// <returns>The command outcome with a presenter-facing message.</returns>
    public async Task<VsCodeCommandResult> InstallExtensionAsync(CancellationToken cancellationToken)
    {
        var vsixPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, VsixRelativePath));

        if (!File.Exists(vsixPath))
        {
            return new VsCodeCommandResult(false, $"Extension package not found: {vsixPath}");
        }

        var result = await RunCodeCliAsync($"--install-extension \"{vsixPath}\"", cancellationToken);
        VsCodeAttachLog.InstallCompleted(logger, result.Succeeded);

        return result.Succeeded
            ? new VsCodeCommandResult(true, "Demo-attach extension installed.")
            : new VsCodeCommandResult(false, $"Extension install failed: {Summarize(result.Output)}");
    }

    /// <summary>
    /// Asks VS Code to attach the .NET debugger to the running Operations Agent service.
    /// </summary>
    /// <param name="cancellationToken">Cancels the CLI call.</param>
    /// <returns>The command outcome with a presenter-facing message.</returns>
    public async Task<VsCodeCommandResult> RequestAttachAsync(CancellationToken cancellationToken)
    {
        var attachUri = $"vscode://{ExtensionId}/attach?processName={OperationsAgentProcessName}";
        var result = await RunCodeCliAsync($"--open-url \"{attachUri}\"", cancellationToken);
        VsCodeAttachLog.AttachRequested(logger, result.Succeeded);

        return result.Succeeded
            ? new VsCodeCommandResult(true, "Attach requested. Watch for the \"Debugger attached\" stamp.")
            : new VsCodeCommandResult(false, $"Attach request failed: {Summarize(result.Output)}");
    }

    private static async Task<(bool Succeeded, string Output)> RunCodeCliAsync(string arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            Arguments = $"/c code {arguments}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);

        if (process is null)
        {
            return (false, "The VS Code CLI could not be started.");
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(CliTimeout);

        try
        {
            var standardOutput = await process.StandardOutput.ReadToEndAsync(timeoutSource.Token);
            var standardError = await process.StandardError.ReadToEndAsync(timeoutSource.Token);
            await process.WaitForExitAsync(timeoutSource.Token);
            return (process.ExitCode == 0, $"{standardOutput}\n{standardError}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            return (false, "The VS Code CLI call timed out.");
        }
    }

    private static string Summarize(string output)
    {
        var lastLine = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();
        return string.IsNullOrWhiteSpace(lastLine) ? "no CLI output" : lastLine;
    }
}

/// <summary>
/// Reports whether the VS Code CLI is reachable and whether the demo-attach extension is installed.
/// </summary>
/// <param name="VsCodeAvailable">Whether the <c>code</c> CLI responded.</param>
/// <param name="ExtensionInstalled">Whether the demo-attach extension is installed.</param>
internal sealed record VsCodeAttachStatus(bool VsCodeAvailable, bool ExtensionInstalled);

/// <summary>
/// Reports the outcome of one VS Code CLI command.
/// </summary>
/// <param name="Succeeded">Whether the command succeeded.</param>
/// <param name="Message">A presenter-facing outcome message.</param>
internal sealed record VsCodeCommandResult(bool Succeeded, string Message);

internal static partial class VsCodeAttachLog
{
    [LoggerMessage(
        EventId = 2599,
        Level = LogLevel.Debug,
        Message = "VS Code attach tooling status. VsCodeAvailable: {VsCodeAvailable}, ExtensionInstalled: {ExtensionInstalled}.")]
    internal static partial void StatusChecked(ILogger logger, bool vsCodeAvailable, bool extensionInstalled);

    [LoggerMessage(
        EventId = 2600,
        Level = LogLevel.Information,
        Message = "Demo-attach extension install completed. Succeeded: {Succeeded}.")]
    internal static partial void InstallCompleted(ILogger logger, bool succeeded);

    [LoggerMessage(
        EventId = 2601,
        Level = LogLevel.Information,
        Message = "VS Code debugger attach requested. Succeeded: {Succeeded}.")]
    internal static partial void AttachRequested(ILogger logger, bool succeeded);
}
