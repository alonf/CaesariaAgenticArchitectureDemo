using System.Diagnostics;
using System.Text.RegularExpressions;

namespace DemoControl.Web.Services;

/// <summary>
/// Drives the presenter-machine VS Code instance for the demo-breakpoint feature: detects the
/// companion demo-attach extension and its version, installs or updates it from the repository
/// .vsix, and asks VS Code to attach the .NET debugger to one demo service or to detach from it.
/// </summary>
internal sealed partial class VsCodeAttachService(IWebHostEnvironment environment, ILogger<VsCodeAttachService> logger)
{
    internal const string ExtensionId = "caesarea-demo.demo-attach";
    private const string ExtensionFolderRelativePath = "../../tools/vscode-demo-attach";
    private const string VsixSearchPattern = "demo-attach-*.vsix";
    private static readonly TimeSpan CliTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets whether the VS Code CLI is reachable, whether the demo-attach extension is installed,
    /// and how its version compares with the package committed in the repository.
    /// </summary>
    /// <param name="cancellationToken">Cancels the CLI call.</param>
    /// <returns>The current attach tooling status.</returns>
    public async Task<VsCodeAttachStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        var result = await RunCodeCliAsync("--list-extensions --show-versions", cancellationToken);

        if (!result.Succeeded)
        {
            VsCodeAttachLog.StatusChecked(logger, vsCodeAvailable: false, extensionInstalled: false);
            return new VsCodeAttachStatus(VsCodeAvailable: false, ExtensionInstalled: false);
        }

        var (installed, installedVersion) = ParseInstalledExtension(result.Output);
        var packaged = FindPackagedVsix();

        VsCodeAttachLog.StatusChecked(logger, vsCodeAvailable: true, extensionInstalled: installed);
        return new VsCodeAttachStatus(VsCodeAvailable: true, installed, installedVersion, packaged.Version?.ToString());
    }

    /// <summary>
    /// Installs the demo-attach extension from the repository .vsix package, replacing whatever
    /// version VS Code already has.
    /// </summary>
    /// <param name="cancellationToken">Cancels the CLI call.</param>
    /// <returns>The command outcome with a presenter-facing message.</returns>
    public async Task<VsCodeCommandResult> InstallExtensionAsync(CancellationToken cancellationToken)
    {
        var (vsixPath, version) = FindPackagedVsix();

        if (vsixPath is null)
        {
            return new VsCodeCommandResult(false, $"No {VsixSearchPattern} package found under {ExtensionFolder}.");
        }

        // --force lets the committed package replace an older install without a prompt nobody is
        // there to answer; the same command serves a first install.
        var result = await RunCodeCliAsync($"--install-extension \"{vsixPath}\" --force", cancellationToken);
        VsCodeAttachLog.InstallCompleted(logger, result.Succeeded);

        // A VS Code window that already activated the previous version keeps running it until the
        // window reloads, and the old version silently ignores routes it does not know.
        return result.Succeeded
            ? new VsCodeCommandResult(true, $"Demo-attach extension {version} installed. Reload the VS Code window (Developer: Reload Window) before attaching, or the previous version keeps answering.")
            : new VsCodeCommandResult(false, $"Extension install failed: {Summarize(result.Output)}");
    }

    /// <summary>
    /// Asks VS Code to attach the .NET debugger to one running demo service. Snippets live in the
    /// service whose code they pause, so the process to attach to is named by the caller rather
    /// than fixed to the Operations Agent.
    /// </summary>
    /// <param name="processName">The service process to attach to, for example OperationsAgent.Api.exe.</param>
    /// <param name="cancellationToken">Cancels the CLI call.</param>
    /// <returns>The command outcome with a presenter-facing message.</returns>
    public Task<VsCodeCommandResult> RequestAttachAsync(string processName, CancellationToken cancellationToken) =>
        RequestDebuggerAsync("attach", "Attach", processName, cancellationToken);

    /// <summary>
    /// Asks VS Code to detach the .NET debugger from one demo service. The service keeps running;
    /// only the debugger lets go.
    /// </summary>
    /// <param name="processName">The service process to detach from, for example OperationsAgent.Api.exe.</param>
    /// <param name="cancellationToken">Cancels the CLI call.</param>
    /// <returns>The command outcome with a presenter-facing message.</returns>
    public Task<VsCodeCommandResult> RequestDetachAsync(string processName, CancellationToken cancellationToken) =>
        RequestDebuggerAsync("detach", "Detach", processName, cancellationToken);

    /// <summary>
    /// Reads the demo-attach extension's presence and version from <c>code --list-extensions --show-versions</c> output.
    /// </summary>
    /// <param name="listOutput">The CLI output, one <c>publisher.name@version</c> per line.</param>
    /// <returns>Whether the extension is installed, and its version when the CLI printed one.</returns>
    internal static (bool Installed, string? Version) ParseInstalledExtension(string listOutput)
    {
        ArgumentNullException.ThrowIfNull(listOutput);

        foreach (var line in listOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = line.IndexOf('@');
            var id = separator < 0 ? line : line[..separator];

            if (string.Equals(id, ExtensionId, StringComparison.OrdinalIgnoreCase))
            {
                var version = separator < 0 ? null : line[(separator + 1)..].Trim();
                return (true, string.IsNullOrEmpty(version) ? null : version);
            }
        }

        return (false, null);
    }

    /// <summary>
    /// Reads the packaged extension version from a <c>demo-attach-&lt;version&gt;.vsix</c> file name.
    /// </summary>
    /// <param name="vsixFileName">The package file name.</param>
    /// <returns>The version, or <see langword="null"/> when the name does not carry one.</returns>
    internal static Version? TryParsePackagedVersion(string vsixFileName)
    {
        ArgumentNullException.ThrowIfNull(vsixFileName);

        var match = VsixNameRegex().Match(vsixFileName);
        return match.Success && Version.TryParse(match.Groups["version"].Value, out var version) ? version : null;
    }

    /// <summary>
    /// Decides whether the committed package should replace the installed extension: only when
    /// both versions are known and the package is newer.
    /// </summary>
    /// <param name="installedVersion">The version VS Code reports, if any.</param>
    /// <param name="packagedVersion">The version of the committed package, if any.</param>
    /// <returns><see langword="true"/> when the package is newer than the install.</returns>
    internal static bool IsUpdateAvailable(string? installedVersion, string? packagedVersion) =>
        Version.TryParse(installedVersion, out var installed)
        && Version.TryParse(packagedVersion, out var packaged)
        && packaged > installed;

    private string ExtensionFolder => Path.GetFullPath(Path.Combine(environment.ContentRootPath, ExtensionFolderRelativePath));

    private (string? Path, Version? Version) FindPackagedVsix()
    {
        if (!Directory.Exists(ExtensionFolder))
        {
            return (null, null);
        }

        return Directory.EnumerateFiles(ExtensionFolder, VsixSearchPattern)
            .Select(path => (Path: (string?)path, Version: TryParsePackagedVersion(Path.GetFileName(path))))
            .Where(candidate => candidate.Version is not null)
            .OrderByDescending(candidate => candidate.Version)
            .FirstOrDefault();
    }

    private async Task<VsCodeCommandResult> RequestDebuggerAsync(string route, string verb, string processName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);

        var uri = $"vscode://{ExtensionId}/{route}?processName={Uri.EscapeDataString(processName)}";
        var result = await RunCodeCliAsync($"--open-url \"{uri}\"", cancellationToken);
        VsCodeAttachLog.DebuggerRequested(logger, route, result.Succeeded);

        return result.Succeeded
            ? new VsCodeCommandResult(true, $"{verb} requested from VS Code.")
            : new VsCodeCommandResult(false, $"{verb} request failed: {Summarize(result.Output)}");
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

    [GeneratedRegex(@"^demo-attach-(?<version>\d+\.\d+\.\d+)\.vsix$", RegexOptions.IgnoreCase)]
    private static partial Regex VsixNameRegex();
}

/// <summary>
/// Reports whether the VS Code CLI is reachable, whether the demo-attach extension is installed,
/// and how its version compares with the package committed in the repository.
/// </summary>
/// <param name="VsCodeAvailable">Whether the <c>code</c> CLI responded.</param>
/// <param name="ExtensionInstalled">Whether the demo-attach extension is installed.</param>
/// <param name="InstalledVersion">The installed extension's version, when VS Code reported one.</param>
/// <param name="PackagedVersion">The version of the .vsix committed in the repository, when one was found.</param>
internal sealed record VsCodeAttachStatus(
    bool VsCodeAvailable,
    bool ExtensionInstalled,
    string? InstalledVersion = null,
    string? PackagedVersion = null)
{
    /// <summary>
    /// Gets whether the committed package is newer than the installed extension, so the
    /// switchboard should offer an update before relying on routes the old version lacks.
    /// </summary>
    public bool UpdateAvailable => ExtensionInstalled && VsCodeAttachService.IsUpdateAvailable(InstalledVersion, PackagedVersion);
}

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
        Message = "VS Code debugger {Route} requested. Succeeded: {Succeeded}.")]
    internal static partial void DebuggerRequested(ILogger logger, string route, bool succeeded);
}
