using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;

namespace DemoControl.Web.Services;

/// <summary>Controls only the separate Functions process owned by this local presenter.</summary>
internal interface IRecoveryLocalRuntime
{
    /// <summary>Checks dependencies and starts the owned worker when it is stopped.</summary>
    public Task EnsureRunningAsync(CancellationToken cancellationToken);
    /// <summary>Force-terminates the owned worker tree without touching scheduler or city services.</summary>
    public Task CrashAsync(CancellationToken cancellationToken);
    /// <summary>Gets the owned launcher identifier, or null when it is stopped.</summary>
    public int? ProcessId { get; }
}

internal sealed class RecoveryLocalSettings(IHostEnvironment environment)
{
    internal static readonly Uri FunctionsAddress = new UriBuilder(Uri.UriSchemeHttp, "localhost", 17071).Uri;
    public bool Enabled => environment.IsDevelopment() && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));
    public string Root { get; } = FindRoot(environment.ContentRootPath);
    public string EvidenceDirectory => Path.Combine(Root, "docs", "runbooks", "artifacts", "director-recovery");

    public Dictionary<string, string> Read()
    {
        if (!Enabled)
        {
            throw new InvalidOperationException("Durable recovery process control is available only in local Development.");
        }

        var path = Path.Combine(Root, "Services", "OperationsAgent.Functions", "local.settings.json");
        if (!File.Exists(path))
        {
            throw new InvalidOperationException("One-time setup is missing: follow docs/runbooks/durable-recovery.md to initialize the private Functions settings.");
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var values = document.RootElement.GetProperty("Values").Deserialize<Dictionary<string, string>>()!;
        if (values.GetValueOrDefault("CAESAREA_LOCAL_REHEARSAL") != "true"
            || values.GetValueOrDefault("DURABLE_TASK_SCHEDULER_CONNECTION_STRING") != "Endpoint=http://localhost:18080;TaskHub=caesarea-recovery-v2;Authentication=None")
        {
            throw new InvalidOperationException("The director requires the isolated local caesarea-recovery-v2 scheduler on port 18080.");
        }

        foreach (var name in new[] { "ENERGYHUB_BASE_URI", "COMMANDCENTER_BASE_URI" })
        {
            if (!Uri.TryCreate(values.GetValueOrDefault(name), UriKind.Absolute, out var uri)
                || !uri.IsLoopback || uri.Scheme is not ("http" or "https"))
            {
                throw new InvalidOperationException($"{name} must address the local city.");
            }
        }

        var operatorKey = values.GetValueOrDefault("CAESAREA_OPERATOR_KEY");
        var supervisorKey = values.GetValueOrDefault("CAESAREA_SUPERVISOR_KEY");
        if (operatorKey is not { Length: >= 32 } || supervisorKey is not { Length: >= 32 } || operatorKey == supervisorKey)
        {
            throw new InvalidOperationException("Configure distinct operator and supervisor credentials in private local settings.");
        }

        return values;
    }

    private static string FindRoot(string start)
    {
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Caesarea.slnx")))
            {
                return directory.FullName;
            }
        }

        return start;
    }
}

internal sealed partial class RecoveryLocalRuntime(RecoveryLocalSettings settings, IHttpClientFactory clients,
    ILogger<RecoveryLocalRuntime> logger) : IRecoveryLocalRuntime, IDisposable
{
    private Process? _worker;
    private Task? _output;
    private Task? _errors;

    public int? ProcessId => _worker is { HasExited: false } ? _worker.Id : null;

    public async Task EnsureRunningAsync(CancellationToken cancellationToken)
    {
        settings.Read();
        await EnsureEmulatorAsync("caesarea-recovery-dts-v2", 18080, cancellationToken);
        await EnsureEmulatorAsync("caesarea-recovery-azurite", 10000, cancellationToken);
        if (ProcessId is null)
        {
            if (await PortOpenAsync(17071, cancellationToken))
            {
                throw new InvalidOperationException("Port 17071 belongs to an external Functions host. Stop that host once; the director will then own all subsequent starts and crashes.");
            }

            Directory.CreateDirectory(settings.EvidenceDirectory);
            var start = new ProcessStartInfo(FindExecutable("pwsh"))
            {
                WorkingDirectory = settings.Root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-File", Path.Combine(settings.Root, "scripts", "Start-DurableRecovery.ps1"), "-NoBuild" })
            {
                start.ArgumentList.Add(argument);
            }

            _worker?.Dispose();
            _worker = Process.Start(start) ?? throw new InvalidOperationException("Functions host could not start.");
            var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            _output = CaptureAsync(_worker.StandardOutput, Path.Combine(settings.EvidenceDirectory, $"worker-{stamp}.log"));
            _errors = CaptureAsync(_worker.StandardError, Path.Combine(settings.EvidenceDirectory, $"worker-{stamp}-error.log"));
            WorkerStarted(logger, _worker.Id);
        }

        using var client = clients.CreateClient("recovery-probe");
        for (var attempt = 0; attempt < 90; attempt++)
        {
            if (ProcessId is null)
            {
                throw new InvalidOperationException("Functions exited during startup. Inspect the worker logs in docs/runbooks/artifacts/director-recovery.");
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(RecoveryLocalSettings.FunctionsAddress, "admin/host/status"));
                request.Headers.Add(CorrelationHeaderNames.XCorrelationId, CorrelationIds.Create());
                using var response = await client.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    using var state = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
                    if (state.RootElement.TryGetProperty("state", out var value) && value.GetString() == "Running")
                    {
                        return;
                    }
                }
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                // Core Tools can accept connections before it has finished indexing the worker.
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        throw new TimeoutException("Functions did not become ready within the startup budget. Inspect the local worker logs.");
    }

    public async Task CrashAsync(CancellationToken cancellationToken)
    {
        settings.Read();
        if (_worker is not { HasExited: false })
        {
            throw new InvalidOperationException("There is no live Functions worker owned by this director.");
        }

        WorkerCrashed(logger, _worker.Id);
        _worker.Kill(entireProcessTree: true);
        await _worker.WaitForExitAsync(cancellationToken);
        await Task.WhenAll(_output ?? Task.CompletedTask, _errors ?? Task.CompletedTask).WaitAsync(cancellationToken);
        for (var attempt = 0; attempt < 30 && await PortOpenAsync(17071, cancellationToken); attempt++)
        {
            await Task.Delay(100, cancellationToken);
        }

        if (await PortOpenAsync(17071, cancellationToken))
        {
            throw new InvalidOperationException("The Functions port remains occupied; no restart was attempted.");
        }
    }

    private static async Task CaptureAsync(StreamReader reader, string path)
    {
        await using var writer = new StreamWriter(path, append: false) { AutoFlush = true };
        while (await reader.ReadLineAsync() is { } line)
        {
            await writer.WriteLineAsync(line);
        }
    }

    private static async Task EnsureEmulatorAsync(string name, int port, CancellationToken cancellationToken)
    {
        if (await PortOpenAsync(port, cancellationToken))
        {
            return;
        }

        var start = new ProcessStartInfo(FindExecutable("docker")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("start");
        start.ArgumentList.Add(name);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Docker could not start.");
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        await Task.WhenAll(output, error);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Start Docker Desktop and complete the one-time emulator setup in the runbook ({name}). Existing container data is preserved.");
        }

        for (var attempt = 0; attempt < 30; attempt++)
        {
            if (await PortOpenAsync(port, cancellationToken))
            {
                return;
            }

            await Task.Delay(1000, cancellationToken);
        }

        throw new TimeoutException($"The {name} emulator did not start.");
    }

    private static async Task<bool> PortOpenAsync(int port, CancellationToken cancellationToken)
    {
        using var socket = new TcpClient();
        try
        {
            await socket.ConnectAsync("127.0.0.1", port, cancellationToken);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static string FindExecutable(string name)
    {
        var filename = OperatingSystem.IsWindows() ? name + ".exe" : name;
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator).Where(Path.IsPathFullyQualified))
        {
            var path = Path.Combine(directory, filename);
            if (File.Exists(path)) { return path; }
        }

        throw new InvalidOperationException($"Install {name} and make it available on PATH before the rehearsal.");
    }

    public void Dispose()
    {
        if (_worker is { HasExited: false })
        {
            _worker.Kill(entireProcessTree: true);
        }

        _worker?.Dispose();
    }

    [LoggerMessage(2500, LogLevel.Information, "Started owned recovery worker launcher {ProcessId}.")]
    private static partial void WorkerStarted(ILogger logger, int processId);
    [LoggerMessage(2501, LogLevel.Information, "Simulating process loss for owned recovery worker launcher {ProcessId}; scheduler and city remain running.")]
    private static partial void WorkerCrashed(ILogger logger, int processId);
}
