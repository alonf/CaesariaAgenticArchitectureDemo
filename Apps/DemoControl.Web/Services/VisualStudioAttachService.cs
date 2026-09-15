using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace DemoControl.Web.Services;

/// <summary>
/// Drives Visual Studio 2026 on a Windows presenter machine through the helper executable under
/// <c>tools/visualstudio-demo-attach</c>. Everything Visual Studio-specific - COM, the DTE, the
/// running-object table - lives in that helper; this class starts a process and reads JSON, so the
/// switchboard stays portable and simply reports the IDE as unavailable on macOS and Linux.
/// </summary>
internal sealed class VisualStudioAttachService(IWebHostEnvironment environment, ILogger<VisualStudioAttachService> logger) : IDebuggerAdapter
{
    internal const string AdapterId = "visualstudio";
    private const string HelperFolderRelativePath = "../../tools/visualstudio-demo-attach";
    private const string HelperProjectRelativePath = "src/VisualStudioDemoAttach.csproj";
    private const string HelperExecutableRelativePath = "dist/VisualStudioDemoAttach.exe";
    private const string SolutionRelativePath = "../../Caesarea.slnx";
    private static readonly TimeSpan HelperTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    // The devenv that attached each service: detach must go to that window, not to whichever
    // window qualifies at that moment, or a second Visual Studio opened since would be asked to
    // let go of a process it never held.
    private readonly ConcurrentDictionary<string, int> _instanceByProcess = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public string Id => AdapterId;

    /// <inheritdoc />
    public string DisplayName => "Visual Studio 2026";

    /// <inheritdoc />
    public async Task<DebuggerAvailability> GetAvailabilityAsync(CancellationToken cancellationToken)
    {
        var availability = await CheckAvailabilityAsync(cancellationToken);
        DebuggerIntegrationLog.AvailabilityChecked(logger, DisplayName, availability.Available, availability.Summary);
        return availability;
    }

    /// <inheritdoc />
    public async Task<DebuggerCommandResult> SetUpAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new DebuggerCommandResult(false, "The Visual Studio helper builds on Windows only.");
        }

        var projectPath = Path.GetFullPath(Path.Combine(HelperFolder, HelperProjectRelativePath));

        if (!File.Exists(projectPath))
        {
            return new DebuggerCommandResult(false, $"The Visual Studio helper project was not found at {projectPath}.");
        }

        var result = await RunAsync("dotnet", ["build", projectPath, "--configuration", "Release", "--nologo"], BuildTimeout, cancellationToken);
        DebuggerIntegrationLog.SetupCompleted(logger, DisplayName, result.Succeeded ? "Success" : "Failed");

        return result.Succeeded
            ? new DebuggerCommandResult(true, "Visual Studio helper built.")
            : new DebuggerCommandResult(false, $"Visual Studio helper build failed: {Summarize(result.Output)}");
    }

    /// <inheritdoc />
    public Task<DebuggerCommandResult> AttachAsync(DebuggerTarget target, CancellationToken cancellationToken) =>
        RequestDebuggerAsync("attach", "Attach", target, cancellationToken);

    /// <inheritdoc />
    public Task<DebuggerCommandResult> DetachAsync(DebuggerTarget target, CancellationToken cancellationToken) =>
        RequestDebuggerAsync("detach", "Detach", target, cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// The helper's status lists the processes the Visual Studio instance debugs, so this answer
    /// is exact whenever the instance with the solution open can be asked.
    /// </remarks>
    public async Task<bool?> IsAttachedAsync(DebuggerTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (!OperatingSystem.IsWindows() || !File.Exists(HelperExecutable) || target.ProcessId is not { } processId)
        {
            return null;
        }

        var report = await RunHelperAsync(
            ["status", "--solution", SolutionPath, "--process-name", target.ProcessName, "--process-id", processId.ToString(System.Globalization.CultureInfo.InvariantCulture)],
            cancellationToken);

        return report.Succeeded ? report.Attached : null;
    }

    /// <summary>
    /// Reads the helper's JSON report. The helper prints one on every exit, success or failure;
    /// anything else on standard output is a crash, reported as such rather than hidden.
    /// </summary>
    /// <param name="output">The helper's standard output.</param>
    /// <returns>The report, or a failed report carrying the raw output.</returns>
    internal static VisualStudioHelperReport ParseReport(string output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var json = output.Trim();

        if (json.StartsWith('{'))
        {
            try
            {
                var report = JsonSerializer.Deserialize<VisualStudioHelperReport>(json, SerializerOptions);

                if (report is not null)
                {
                    return report;
                }
            }
            catch (JsonException)
            {
                // Fall through: the raw output is the best diagnostic there is.
            }
        }

        return new VisualStudioHelperReport(false, "unknown", string.IsNullOrWhiteSpace(json) ? "The Visual Studio helper produced no report." : Summarize(json), false, null);
    }

    /// <summary>
    /// Turns a status report into what the debugger picker shows.
    /// </summary>
    /// <param name="report">The helper's status report.</param>
    /// <returns>The availability with the instance found, or the reason none qualifies.</returns>
    internal static DebuggerAvailability Describe(VisualStudioHelperReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (report.Succeeded && report.Instance is { } instance)
        {
            return new DebuggerAvailability(true, $"{instance.Version} · PID {instance.ProcessId}", $"{instance.DisplayName} has the Caesarea solution open.");
        }

        return new DebuggerAvailability(false, "solution not open", report.Message);
    }

    private string HelperFolder => Path.GetFullPath(Path.Combine(environment.ContentRootPath, HelperFolderRelativePath));

    private string HelperExecutable => Path.GetFullPath(Path.Combine(HelperFolder, HelperExecutableRelativePath));

    private string SolutionPath => Path.GetFullPath(Path.Combine(environment.ContentRootPath, SolutionRelativePath));

    private async Task<DebuggerAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new DebuggerAvailability(false, "not on this platform", "Visual Studio is unavailable on macOS and Linux; use VS Code.");
        }

        if (!File.Exists(HelperExecutable))
        {
            return new DebuggerAvailability(
                false,
                "helper not built",
                "The Visual Studio helper under tools/visualstudio-demo-attach is not built yet.",
                "Build Visual Studio helper");
        }

        var report = await RunHelperAsync(["status", "--solution", SolutionPath], cancellationToken);
        return Describe(report);
    }

    private async Task<DebuggerCommandResult> RequestDebuggerAsync(string operation, string verb, DebuggerTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (!OperatingSystem.IsWindows())
        {
            return new DebuggerCommandResult(false, "Visual Studio debugging is unavailable on this platform. Use VS Code instead.");
        }

        if (!File.Exists(HelperExecutable))
        {
            return new DebuggerCommandResult(false, "The Visual Studio helper is not built. Build it from the debugger picker first.");
        }

        List<string> arguments = [operation, "--solution", SolutionPath, "--process-name", target.ProcessName];

        if (target.ProcessId is { } processId)
        {
            arguments.AddRange(["--process-id", processId.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        }

        if (operation == "detach" && _instanceByProcess.TryGetValue(target.ProcessName, out var instanceProcessId))
        {
            arguments.AddRange(["--instance-pid", instanceProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        }

        var report = await RunHelperAsync(arguments, cancellationToken);

        // The helper's report is the ownership proof: it says attached only once the chosen
        // Visual Studio lists the process among the ones it debugs, and detached only once it no
        // longer does.
        if (report.Succeeded && report.Attached == (operation == "attach"))
        {
            if (operation == "attach" && report.Instance is { } instance)
            {
                _instanceByProcess[target.ProcessName] = instance.ProcessId;
            }
            else
            {
                _instanceByProcess.TryRemove(target.ProcessName, out _);
            }

            DebuggerIntegrationLog.DebuggerRequested(logger, DisplayName, verb, target.ProcessName, target.ProcessId, "Success");
            return new DebuggerCommandResult(true, report.Message);
        }

        DebuggerIntegrationLog.DebuggerRequestFailed(logger, DisplayName, verb, target.ProcessName, target.ProcessId, report.Message);
        return new DebuggerCommandResult(false, $"Visual Studio {verb.ToLowerInvariant()} failed. The service keeps running. {report.Message}");
    }

    private async Task<VisualStudioHelperReport> RunHelperAsync(List<string> arguments, CancellationToken cancellationToken)
    {
        var result = await RunAsync(HelperExecutable, arguments, HelperTimeout, cancellationToken);

        // The helper reports failures in the JSON too, with a non-zero exit code; only a start or
        // timeout failure has no report to read.
        return result.Started ? ParseReport(result.Output) : new VisualStudioHelperReport(false, arguments[0], result.Output, false, null);
    }

    private static async Task<(bool Started, bool Succeeded, string Output)> RunAsync(
        string fileName, List<string> arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        Process? process;

        try
        {
            process = Process.Start(startInfo);
        }
        catch (Win32Exception exception)
        {
            return (false, false, $"{Path.GetFileName(fileName)} could not be started: {exception.Message}");
        }

        if (process is null)
        {
            return (false, false, $"{Path.GetFileName(fileName)} could not be started.");
        }

        using (process)
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);

            try
            {
                var standardOutput = await process.StandardOutput.ReadToEndAsync(timeoutSource.Token);
                var standardError = await process.StandardError.ReadToEndAsync(timeoutSource.Token);
                await process.WaitForExitAsync(timeoutSource.Token);
                return (true, process.ExitCode == 0, string.IsNullOrWhiteSpace(standardOutput) ? standardError : standardOutput);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                process.Kill(entireProcessTree: true);
                return (false, false, $"{Path.GetFileName(fileName)} did not finish within {timeout.TotalSeconds:0} seconds.");
            }
        }
    }

    private static string Summarize(string output)
    {
        var lastLine = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault(line => !line.StartsWith("Time Elapsed", StringComparison.Ordinal));
        return string.IsNullOrWhiteSpace(lastLine) ? "no output" : lastLine;
    }
}

/// <summary>
/// The report the Visual Studio helper prints as JSON on every exit.
/// </summary>
/// <param name="Succeeded">Whether the operation succeeded.</param>
/// <param name="Operation">The operation reported: status, attach, detach or instances.</param>
/// <param name="Message">A presenter-facing sentence.</param>
/// <param name="Attached">Whether the target process is debugged by the instance, after the operation.</param>
/// <param name="Instance">The Visual Studio instance that has the solution open, or <see langword="null"/>.</param>
internal sealed record VisualStudioHelperReport(
    bool Succeeded,
    string Operation,
    string Message,
    bool Attached,
    VisualStudioInstanceReport? Instance);

/// <summary>
/// One running Visual Studio instance as the helper saw it.
/// </summary>
/// <param name="DisplayName">The product name, for example Visual Studio 2026 Enterprise.</param>
/// <param name="Version">The product version.</param>
/// <param name="ProcessId">The devenv process id.</param>
/// <param name="Solution">The full path of the open solution, or <see langword="null"/> when none is open.</param>
internal sealed record VisualStudioInstanceReport(string DisplayName, string Version, int ProcessId, string? Solution);
