using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace VisualStudioDemoAttach;

/// <summary>
/// Entry point. Reads one operation from the command line, drives the Visual Studio instance that
/// has the Caesarea solution open, and prints one JSON report. Exit code 0 on success, 1 when the
/// operation failed, 2 when the command line was wrong.
/// </summary>
internal static class Program
{
    private const int RpcCallRejected = unchecked((int)0x80010001);
    private const int RpcServerCallRetryLater = unchecked((int)0x8001010A);
    private const int BusyRetries = 10;
    private static readonly TimeSpan BusyRetryDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan SettlePoll = TimeSpan.FromMilliseconds(250);

    // The COM message filter needs a single-threaded apartment, and so does the DTE.
    [STAThread]
    private static int Main(string[] args)
    {
        var command = HelperCommand.Parse(args);

        if (command.Error is not null)
        {
            Print(HelperReport.Failed("usage", $"{command.Error} {HelperCommand.Usage}"));
            return 2;
        }

        HelperReport report;

        using (MessageFilter.Register())
        {
            report = RunWithRetry(command);
        }

        Print(report);
        return report.Succeeded ? 0 : 1;
    }

    private static HelperReport RunWithRetry(HelperCommand command)
    {
        var attempt = 0;

        while (true)
        {
            attempt++;

            try
            {
                return Run(command);
            }
            catch (COMException exception) when (attempt < BusyRetries && exception.HResult is RpcCallRejected or RpcServerCallRetryLater)
            {
                // Visual Studio is mid-build or mid-paint; the operations below are idempotent, so
                // the whole thing is simply tried again.
                Thread.Sleep(BusyRetryDelay);
            }
            catch (COMException exception)
            {
                return HelperReport.Failed(command.Operation, $"Visual Studio refused the request: {exception.Message}");
            }
            catch (InvalidCastException exception)
            {
                return HelperReport.Failed(command.Operation, $"The Visual Studio automation object did not answer as expected: {exception.Message}");
            }
        }
    }

    private static HelperReport Run(HelperCommand command)
    {
        var instances = RunningObjectTable.FindVisualStudioInstances()
            .Select(running => new VisualStudioInstance(running))
            .ToList();
        var reports = instances.Select(instance => instance.ToReport()).ToList();

        if (command.Operation == "instances")
        {
            return new HelperReport(true, command.Operation, $"{instances.Count} Visual Studio instance(s) running.", Instances: reports);
        }

        var candidates = instances.Select(instance => instance.ToCandidate()).ToList();
        var chosen = InstanceSelection.Choose(candidates, command.Solution, command.InstanceProcessId, out var explanation);
        var instance = chosen is null ? null : instances.Find(candidate => candidate.ProcessId == chosen.ProcessId);

        if (instance is null)
        {
            return HelperReport.Failed(command.Operation, explanation, reports);
        }

        return command.Operation switch
        {
            "status" => Status(instance, command, reports),
            "attach" => Attach(instance, command),
            "detach" => Detach(instance, command),
            _ => HelperReport.Failed(command.Operation, HelperCommand.Usage)
        };
    }

    private static HelperReport Status(VisualStudioInstance instance, HelperCommand command, IReadOnlyList<VisualStudioInstanceReport> reports)
    {
        var attached = false;
        TargetReport? target = null;

        if (command.ProcessId is not null || command.ProcessName is not null)
        {
            var debugged = instance.FindDebuggedProcess(command.ProcessId, command.ProcessName, out _);
            attached = debugged is not null;
            target = debugged is null ? null : new TargetReport(Path.GetFileName(debugged.Name), debugged.ProcessID);
        }

        var solution = instance.SolutionPath is null ? "no solution" : Path.GetFileName(instance.SolutionPath);
        var message = target is null
            ? $"{instance.DisplayName} {instance.Version} (PID {instance.ProcessId}) has {solution} open."
            : $"{instance.DisplayName} (PID {instance.ProcessId}) is attached to {target.ProcessName} (PID {target.ProcessId}).";

        return new HelperReport(true, command.Operation, message, attached, instance.ToReport(), reports, target);
    }

    private static HelperReport Attach(VisualStudioInstance instance, HelperCommand command)
    {
        var process = instance.FindLocalProcess(command.ProcessId, command.ProcessName, out var problem);

        if (process is null)
        {
            return HelperReport.Failed(command.Operation, problem ?? $"No running process matches {command.DescribeTarget()}.", [instance.ToReport()]);
        }

        var target = new TargetReport(Path.GetFileName(process.Name), process.ProcessID);

        if (instance.IsDebugging(target.ProcessId))
        {
            return new HelperReport(true, command.Operation, $"{instance.DisplayName} is already attached to {target.ProcessName} (PID {target.ProcessId}).", true, instance.ToReport(), Target: target);
        }

        var engine = DebuggerEngines.Choose(instance.EngineNames());
        VisualStudioInstance.Attach(process, engine);

        if (!WaitUntil(() => instance.IsDebugging(target.ProcessId)))
        {
            return HelperReport.Failed(command.Operation, $"{instance.DisplayName} did not report {target.ProcessName} (PID {target.ProcessId}) as debugged within {SettleTimeout.TotalSeconds:0} seconds.", [instance.ToReport()]);
        }

        var engineNote = engine is null ? "the auto-detected engine" : engine;
        return new HelperReport(true, command.Operation, $"{instance.DisplayName} attached to {target.ProcessName} (PID {target.ProcessId}) with {engineNote}.", true, instance.ToReport(), Target: target);
    }

    private static HelperReport Detach(VisualStudioInstance instance, HelperCommand command)
    {
        var process = instance.FindDebuggedProcess(command.ProcessId, command.ProcessName, out var problem);

        if (problem is not null)
        {
            return HelperReport.Failed(command.Operation, problem, [instance.ToReport()]);
        }

        if (process is null)
        {
            return new HelperReport(true, command.Operation, $"{instance.DisplayName} is not attached to {command.DescribeTarget()}; nothing to detach.", false, instance.ToReport());
        }

        // Read the identity before letting go: the automation object may be gone right after.
        var target = new TargetReport(Path.GetFileName(process.Name), process.ProcessID);
        VisualStudioInstance.Detach(process);

        if (!WaitUntil(() => !instance.IsDebugging(target.ProcessId)))
        {
            return HelperReport.Failed(command.Operation, $"{instance.DisplayName} still reports {target.ProcessName} (PID {target.ProcessId}) as debugged after {SettleTimeout.TotalSeconds:0} seconds.", [instance.ToReport()]);
        }

        return new HelperReport(true, command.Operation, $"{instance.DisplayName} detached from {target.ProcessName} (PID {target.ProcessId}); the process keeps running.", false, instance.ToReport(), Target: target);
    }

    private static bool WaitUntil(Func<bool> condition)
    {
        var stopwatch = Stopwatch.StartNew();

        while (!condition())
        {
            if (stopwatch.Elapsed >= SettleTimeout)
            {
                return false;
            }

            Thread.Sleep(SettlePoll);
        }

        return true;
    }

    private static void Print(HelperReport report) =>
        Console.Out.WriteLine(JsonSerializer.Serialize(report, HelperReportJsonContext.Default.HelperReport));
}
