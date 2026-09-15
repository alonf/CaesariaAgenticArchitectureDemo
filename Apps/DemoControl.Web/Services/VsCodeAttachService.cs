using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace DemoControl.Web.Services;

/// <summary>
/// Drives the presenter-machine VS Code instance for the demo-breakpoint feature: detects the
/// companion demo-attach extension and its version, installs or updates it from the repository
/// .vsix, and asks VS Code to attach the .NET debugger to one demo service or to detach from it.
/// Runs wherever VS Code does - Windows, macOS, Linux - through the <c>code</c> CLI.
/// </summary>
internal sealed partial class VsCodeAttachService(IWebHostEnvironment environment, ILogger<VsCodeAttachService> logger) : IDebuggerAdapter
{
    internal const string AdapterId = "vscode";
    internal const string ExtensionId = "caesarea-demo.demo-attach";
    private const string ExtensionFolderRelativePath = "../../tools/vscode-demo-attach";
    private const string VsixSearchPattern = "demo-attach-*.vsix";
    private static readonly TimeSpan CliTimeout = TimeSpan.FromSeconds(30);

    // Where the code CLI lives when it is not on PATH: the macOS app bundle, and the Linux
    // package and snap layouts. Windows always goes through PATH, because the installer puts it there.
    private static readonly string[] WellKnownCliPaths =
    [
        "/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code",
        "/usr/local/bin/code",
        "/usr/bin/code",
        "/snap/bin/code",
        "/usr/share/code/bin/code"
    ];

    /// <inheritdoc />
    public string Id => AdapterId;

    /// <inheritdoc />
    public string DisplayName => "VS Code";

    /// <summary>
    /// Gets whether the VS Code CLI is reachable, whether the demo-attach extension is installed,
    /// and how its version compares with the package committed in the repository.
    /// </summary>
    /// <param name="cancellationToken">Cancels the CLI call.</param>
    /// <returns>The current attach tooling status.</returns>
    public async Task<VsCodeAttachStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        var result = await RunCodeCliAsync(["--list-extensions", "--show-versions"], cancellationToken);

        if (!result.Succeeded)
        {
            return new VsCodeAttachStatus(VsCodeAvailable: false, ExtensionInstalled: false);
        }

        var (installed, installedVersion) = ParseInstalledExtension(result.Output);
        var packaged = FindPackagedVsix();

        return new VsCodeAttachStatus(VsCodeAvailable: true, installed, installedVersion, packaged.Version?.ToString());
    }

    /// <inheritdoc />
    public async Task<DebuggerAvailability> GetAvailabilityAsync(CancellationToken cancellationToken)
    {
        var status = await GetStatusAsync(cancellationToken);
        var availability = Describe(status);
        DebuggerIntegrationLog.AvailabilityChecked(logger, DisplayName, availability.Available, availability.Summary);
        return availability;
    }

    /// <inheritdoc />
    public Task<DebuggerCommandResult> SetUpAsync(CancellationToken cancellationToken) => InstallExtensionAsync(cancellationToken);

    /// <summary>
    /// Installs the demo-attach extension from the repository .vsix package, replacing whatever
    /// version VS Code already has.
    /// </summary>
    /// <param name="cancellationToken">Cancels the CLI call.</param>
    /// <returns>The command outcome with a presenter-facing message.</returns>
    public async Task<DebuggerCommandResult> InstallExtensionAsync(CancellationToken cancellationToken)
    {
        var (vsixPath, version) = FindPackagedVsix();

        if (vsixPath is null)
        {
            return new DebuggerCommandResult(false, $"No {VsixSearchPattern} package found under {ExtensionFolder}.");
        }

        // --force lets the committed package replace an older install without a prompt nobody is
        // there to answer; the same command serves a first install.
        var result = await RunCodeCliAsync(["--install-extension", vsixPath, "--force"], cancellationToken);
        DebuggerIntegrationLog.SetupCompleted(logger, DisplayName, result.Succeeded ? "Success" : "Failed");

        // A VS Code window that already activated the previous version keeps running it until the
        // window reloads, and the old version silently ignores routes it does not know.
        return result.Succeeded
            ? new DebuggerCommandResult(true, $"Demo-attach extension {version} installed. Reload the VS Code window (Developer: Reload Window) before attaching, or the previous version keeps answering.")
            : new DebuggerCommandResult(false, $"Extension install failed: {Summarize(result.Output)}");
    }

    /// <inheritdoc />
    public Task<DebuggerCommandResult> AttachAsync(DebuggerTarget target, CancellationToken cancellationToken) =>
        RequestDebuggerAsync("attach", "Attach", target, cancellationToken);

    /// <inheritdoc />
    public Task<DebuggerCommandResult> DetachAsync(DebuggerTarget target, CancellationToken cancellationToken) =>
        RequestDebuggerAsync("detach", "Detach", target, cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// VS Code exposes its debug sessions to extensions inside the window only; from outside, the
    /// CLI can hand it a request but cannot ask what it holds. Unknown, therefore.
    /// </remarks>
    public Task<bool?> IsAttachedAsync(DebuggerTarget target, CancellationToken cancellationToken) => Task.FromResult<bool?>(null);

    /// <summary>
    /// Builds the URI the extension answers: the route, the process name the debug session is
    /// named after, and the process id when the service reported one so the extension needs no
    /// platform-specific lookup.
    /// </summary>
    /// <param name="route">The extension route, <c>attach</c> or <c>detach</c>.</param>
    /// <param name="target">The service process.</param>
    /// <returns>The <c>vscode://</c> URI.</returns>
    internal static string BuildRequestUri(string route, DebuggerTarget target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(target.ProcessName);

        var uri = $"vscode://{ExtensionId}/{route}?processName={Uri.EscapeDataString(target.ProcessName)}";
        return target.ProcessId is { } processId ? $"{uri}&processId={processId}" : uri;
    }

    /// <summary>
    /// Turns the tooling status into what the debugger picker shows.
    /// </summary>
    /// <param name="status">The tooling status.</param>
    /// <returns>The availability, with the install or update the presenter can run from here.</returns>
    internal static DebuggerAvailability Describe(VsCodeAttachStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        if (!status.VsCodeAvailable)
        {
            return new DebuggerAvailability(false, "CLI not found", "The VS Code CLI (code) was not found on this machine.");
        }

        if (!status.ExtensionInstalled)
        {
            return new DebuggerAvailability(false, "extension missing", "The demo-attach VS Code extension is not installed.", "Install VS Code extension");
        }

        if (status.UpdateAvailable)
        {
            return new DebuggerAvailability(
                false,
                $"extension {status.InstalledVersion}, {status.PackagedVersion} available",
                "An older demo-attach extension is installed; it does not answer every route this switchboard uses.",
                $"Update VS Code extension {status.InstalledVersion} to {status.PackagedVersion}");
        }

        return new DebuggerAvailability(true, $"extension {status.InstalledVersion}");
    }

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

    /// <summary>
    /// Finds the <c>code</c> CLI on a platform where it may not be on PATH. Windows always resolves
    /// it through PATH, so this answers only for macOS and Linux.
    /// </summary>
    /// <param name="pathVariable">The PATH environment variable.</param>
    /// <param name="fileExists">Whether a candidate path exists.</param>
    /// <returns>The command to start: <c>code</c> when PATH has it, a well-known path otherwise, or <see langword="null"/>.</returns>
    internal static string? LocateCli(string? pathVariable, Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(fileExists);

        var onPath = (pathVariable ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(directory => fileExists(Path.Combine(directory, "code")));

        if (onPath)
        {
            return "code";
        }

        return WellKnownCliPaths.FirstOrDefault(fileExists);
    }

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

    private async Task<DebuggerCommandResult> RequestDebuggerAsync(string route, string verb, DebuggerTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        var result = await RunCodeCliAsync(["--open-url", BuildRequestUri(route, target)], cancellationToken);

        if (result.Succeeded)
        {
            DebuggerIntegrationLog.DebuggerRequested(logger, DisplayName, verb, target.ProcessName, target.ProcessId, "Requested");
            return new DebuggerCommandResult(true, $"{verb} requested from VS Code.");
        }

        var reason = Summarize(result.Output);
        DebuggerIntegrationLog.DebuggerRequestFailed(logger, DisplayName, verb, target.ProcessName, target.ProcessId, reason);
        return new DebuggerCommandResult(false, $"{verb} request failed: {reason}");
    }

    private static async Task<(bool Succeeded, string Output)> RunCodeCliAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var startInfo = CreateStartInfo(arguments);

        if (startInfo is null)
        {
            return (false, "The VS Code CLI (code) was not found.");
        }

        Process? process;

        try
        {
            process = Process.Start(startInfo);
        }
        catch (Win32Exception exception)
        {
            return (false, $"The VS Code CLI could not be started: {exception.Message}");
        }

        if (process is null)
        {
            return (false, "The VS Code CLI could not be started.");
        }

        using (process)
        {
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
    }

    // The CLI is a .cmd shim on Windows, which only cmd.exe can launch, and a shell script
    // elsewhere, which the OS runs directly.
    private static ProcessStartInfo? CreateStartInfo(IReadOnlyList<string> arguments)
    {
        ProcessStartInfo startInfo;

        if (OperatingSystem.IsWindows())
        {
            // Every argument is quoted for cmd.exe: the attach URI carries an '&', which an
            // unquoted cmd line reads as a command separator, and the .vsix path may carry spaces.
            startInfo = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"))
            {
                Arguments = "/c code " + string.Join(' ', arguments.Select(QuoteForCmd))
            };
        }
        else
        {
            var cli = LocateCli(Environment.GetEnvironmentVariable("PATH"), File.Exists);

            if (cli is null)
            {
                return null;
            }

            startInfo = new ProcessStartInfo(cli);

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }
        }

        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        return startInfo;
    }

    /// <summary>
    /// Quotes one argument for a <c>cmd.exe /c</c> command line, so separators such as <c>&amp;</c>
    /// and spaces reach the CLI as text.
    /// </summary>
    /// <param name="argument">The argument; it may not itself contain a double quote.</param>
    /// <returns>The quoted argument.</returns>
    internal static string QuoteForCmd(string argument)
    {
        ArgumentNullException.ThrowIfNull(argument);

        if (argument.Contains('"', StringComparison.Ordinal))
        {
            throw new ArgumentException("A VS Code CLI argument may not contain a double quote.", nameof(argument));
        }

        return $"\"{argument}\"";
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
