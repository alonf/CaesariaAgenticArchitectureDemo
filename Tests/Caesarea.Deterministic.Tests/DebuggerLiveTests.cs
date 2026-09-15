using System.Diagnostics;
using DemoControl.Web.Services;

namespace Caesarea.Deterministic.Tests;

/// <summary>
/// The real round trips: an IDE attaching its .NET debugger to this very test process and letting
/// it go again, observed through <c>Debugger.IsAttached</c> exactly as a demo service observes it,
/// and doing so without disturbing a second process the same IDE is already debugging. They need
/// a presenter machine - VS Code with the demo-attach extension, Visual Studio 2026 with the
/// solution open - so they run only when <c>CAESAREA_LIVE_DEBUGGER_TESTS</c> is set, which
/// <c>scripts/Test-DemoDebuggers.ps1</c> does; anywhere else they skip and say why.
/// </summary>
public sealed class DebuggerLiveTests
{
    private const string OptInVariable = "CAESAREA_LIVE_DEBUGGER_TESTS";
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan SettlePoll = TimeSpan.FromMilliseconds(500);

    [Fact]
    public async Task VsCodeAttachesToThisProcessAndLetsItGo()
    {
        var adapter = await ReadyVsCodeAsync();

        await RoundTripAsync(adapter);
    }

    [Fact]
    public async Task VisualStudio2026AttachesToThisProcessAndLetsItGo()
    {
        var adapter = await ReadyVisualStudioAsync();

        await RoundTripAsync(adapter);
    }

    [Fact]
    public async Task VsCodeLeavesAnUnrelatedSessionAttached()
    {
        var adapter = await ReadyVsCodeAsync();

        await BystanderStaysAttachedAsync(adapter);
    }

    [Fact]
    public async Task VisualStudio2026LeavesAnUnrelatedSessionAttached()
    {
        var adapter = await ReadyVisualStudioAsync();

        await BystanderStaysAttachedAsync(adapter);
    }

    private static async Task<IDebuggerAdapter> ReadyVsCodeAsync()
    {
        SkipUnlessOptedIn();

        var adapter = new VsCodeAttachService(DemoControlEnvironment(), NullLogger<VsCodeAttachService>.Instance);
        var availability = await adapter.GetAvailabilityAsync(TestContext.Current.CancellationToken);
        Assert.SkipUnless(availability.Available, $"VS Code is not ready: {availability.Detail ?? availability.Summary}");
        return adapter;
    }

    private static async Task<IDebuggerAdapter> ReadyVisualStudioAsync()
    {
        SkipUnlessOptedIn();
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Visual Studio runs on Windows only.");

        var adapter = new VisualStudioAttachService(DemoControlEnvironment(), NullLogger<VisualStudioAttachService>.Instance);
        var availability = await adapter.GetAvailabilityAsync(TestContext.Current.CancellationToken);
        Assert.SkipUnless(availability.Available, $"Visual Studio 2026 is not ready: {availability.Detail ?? availability.Summary}");
        return adapter;
    }

    private static async Task RoundTripAsync(IDebuggerAdapter adapter)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Assert.SkipWhen(Debugger.IsAttached, "A debugger is already on the test process; run these tests without one.");

        var target = new DebuggerTarget(Path.GetFileName(Environment.ProcessPath!), Environment.ProcessId);

        var attach = await adapter.AttachAsync(target, cancellationToken);
        Assert.True(attach.Succeeded, attach.Message);
        Assert.True(await WaitAsync(() => Debugger.IsAttached, cancellationToken), $"{adapter.DisplayName} did not attach within {SettleTimeout.TotalSeconds:0} seconds. {attach.Message}");

        var detach = await adapter.DetachAsync(target, cancellationToken);
        Assert.True(detach.Succeeded, detach.Message);
        Assert.True(await WaitAsync(() => !Debugger.IsAttached, cancellationToken), $"{adapter.DisplayName} did not detach within {SettleTimeout.TotalSeconds:0} seconds. {detach.Message}");
    }

    // Attach and detach are per process, by id: two processes with the same name - two checkouts
    // running the same service - must be told apart. With the IDE already on the first, attaching
    // to the second must not be mistaken for "already attached", detaching from the second must
    // leave the first alone, and the first must still be detachable by its own id afterwards.
    private static async Task BystanderStaysAttachedAsync(IDebuggerAdapter adapter)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var first = Bystander.Start();
        using var second = Bystander.Start();
        Assert.SkipWhen(first is null || second is null, "The DebuggerBystander executable is not in the test output; build the test project.");
        Assert.Equal(first.Target.ProcessName, second.Target.ProcessName);
        Assert.NotEqual(first.Target.ProcessId, second.Target.ProcessId);

        var attachFirst = await adapter.AttachAsync(first.Target, cancellationToken);
        Assert.True(attachFirst.Succeeded, attachFirst.Message);
        Assert.True(await WaitAsync(() => first.IsAttached, cancellationToken), $"{adapter.DisplayName} did not attach to the first process. {attachFirst.Message}");

        var attachSecond = await adapter.AttachAsync(second.Target, cancellationToken);
        Assert.True(attachSecond.Succeeded, attachSecond.Message);
        Assert.True(await WaitAsync(() => second.IsAttached, cancellationToken), $"{adapter.DisplayName} did not attach to the second process of the same name. {attachSecond.Message}");

        var detachSecond = await adapter.DetachAsync(second.Target, cancellationToken);
        Assert.True(detachSecond.Succeeded, detachSecond.Message);
        Assert.True(await WaitAsync(() => !second.IsAttached, cancellationToken), $"{adapter.DisplayName} did not detach from the second process. {detachSecond.Message}");

        // The first reports its state every quarter second; give it one more report.
        await Task.Delay(SettlePoll, cancellationToken);
        Assert.True(first.IsAttached, $"{adapter.DisplayName} let go of the first process while detaching from the second.");

        var detachFirst = await adapter.DetachAsync(first.Target, cancellationToken);
        Assert.True(detachFirst.Succeeded, detachFirst.Message);
        Assert.True(await WaitAsync(() => !first.IsAttached, cancellationToken), $"{adapter.DisplayName} did not detach from the first process. {detachFirst.Message}");
    }

    private static async Task<bool> WaitAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        while (!condition())
        {
            if (stopwatch.Elapsed >= SettleTimeout)
            {
                return false;
            }

            await Task.Delay(SettlePoll, cancellationToken);
        }

        return true;
    }

    private static void SkipUnlessOptedIn() =>
        Assert.SkipUnless(
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(OptInVariable)),
            $"Live debugger tests run only with {OptInVariable} set; use scripts/Test-DemoDebuggers.ps1.");

    // The adapters resolve the helper, the extension package and the solution relative to the
    // DemoControl content root, so the test roots them at the real one.
    private static StubWebHostEnvironment DemoControlEnvironment()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Caesarea.slnx")))
        {
            directory = directory.Parent;
        }

        var repositoryRoot = directory?.FullName ?? throw new InvalidOperationException("Repository root could not be located from the test output directory.");
        return new StubWebHostEnvironment(Path.Combine(repositoryRoot, "Apps", "DemoControl.Web"));
    }

    /// <summary>
    /// A second .NET process for the IDE to be attached to: the DebuggerBystander console app
    /// built next to the tests, framework-dependent like the demo services, printing its own
    /// <c>Debugger.IsAttached</c> four times a second so the test can watch it the way it
    /// watches itself.
    /// </summary>
    private sealed class Bystander : IDisposable
    {
        private const string ExecutableName = "Caesarea.DebuggerBystander";
        private readonly Process _process;
        private volatile bool _isAttached;

        private Bystander(Process process)
        {
            _process = process;
            Target = new DebuggerTarget(DemoProcessNames.OnThisPlatform(ExecutableName), process.Id);
            _ = Task.Run(ReadReportsAsync);
        }

        public DebuggerTarget Target { get; }

        public bool IsAttached => _isAttached;

        public static Bystander? Start()
        {
            var executable = Path.Combine(AppContext.BaseDirectory, DemoProcessNames.OnThisPlatform(ExecutableName));

            if (!File.Exists(executable))
            {
                return null;
            }

            var startInfo = new ProcessStartInfo(executable)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                var process = Process.Start(startInfo);
                return process is null ? null : new Bystander(process);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return null;
            }
        }

        public void Dispose()
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // Already gone.
            }

            _process.Dispose();
        }

        private async Task ReadReportsAsync()
        {
            try
            {
                while (await _process.StandardOutput.ReadLineAsync() is { } line)
                {
                    if (bool.TryParse(line.Trim(), out var attached))
                    {
                        _isAttached = attached;
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // The test finished and disposed the process.
            }
        }
    }
}
