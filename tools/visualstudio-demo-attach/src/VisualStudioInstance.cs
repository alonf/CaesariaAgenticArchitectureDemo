using EnvDTE;
using EnvDTE80;

namespace VisualStudioDemoAttach;

/// <summary>
/// One running Visual Studio, driven through its DTE automation object: which solution it has
/// open, which processes it is debugging, and the attach and detach of a single process. Nothing
/// here touches any other process Visual Studio may be debugging.
/// </summary>
internal sealed class VisualStudioInstance
{
    private const string DefaultTransport = "Default";
    private readonly DTE2 _dte;

    /// <summary>
    /// Initializes a new instance of the <see cref="VisualStudioInstance"/> class.
    /// </summary>
    /// <param name="running">The automation object from the running-object table.</param>
    public VisualStudioInstance(RunningVisualStudio running)
    {
        ArgumentNullException.ThrowIfNull(running);

        _dte = (DTE2)running.Dte;
        MajorVersion = running.MajorVersion;
        ProcessId = running.ProcessId;
    }

    /// <summary>
    /// Gets the major version from the moniker, 18 for Visual Studio 2026.
    /// </summary>
    public int MajorVersion { get; }

    /// <summary>
    /// Gets the devenv process id.
    /// </summary>
    public int ProcessId { get; }

    /// <summary>
    /// Gets the product version the instance reports.
    /// </summary>
    public string Version => _dte.Version;

    /// <summary>
    /// Gets the open solution's full path, or <see langword="null"/> when no solution is open.
    /// </summary>
    public string? SolutionPath
    {
        get
        {
            var fullName = _dte.Solution?.FullName;
            return string.IsNullOrWhiteSpace(fullName) ? null : fullName;
        }
    }

    /// <summary>
    /// Gets the product name, for example Visual Studio 2026 Enterprise.
    /// </summary>
    public string DisplayName => $"Visual Studio {ProductYear(MajorVersion)} {_dte.Edition}".Trim();

    /// <summary>
    /// Gets whether the demo attaches through this version.
    /// </summary>
    public bool Supported => MajorVersion >= InstanceSelection.MinimumMajorVersion;

    /// <summary>
    /// Reduces the instance to what selection needs.
    /// </summary>
    /// <returns>The candidate.</returns>
    public InstanceCandidate ToCandidate() => new(MajorVersion, ProcessId, SolutionPath, DisplayName);

    /// <summary>
    /// Reduces the instance to what the report carries.
    /// </summary>
    /// <returns>The report entry.</returns>
    public VisualStudioInstanceReport ToReport() => new(DisplayName, Version, ProcessId, SolutionPath, Supported);

    /// <summary>
    /// Lists the debug engines the default transport offers, by name.
    /// </summary>
    /// <returns>The engine names.</returns>
    public IReadOnlyList<string> EngineNames()
    {
        var engines = Debugger.Transports.Item(DefaultTransport).Engines;
        var names = new List<string>(engines.Count);

        for (var index = 1; index <= engines.Count; index++)
        {
            names.Add(engines.Item(index).Name);
        }

        return names;
    }

    /// <summary>
    /// Finds a local process by id, or by exact executable name when no id is given.
    /// </summary>
    /// <param name="processId">The process id, which wins when given.</param>
    /// <param name="processName">The executable name, for example OperationsAgent.Api.exe.</param>
    /// <param name="problem">Why the lookup was inconclusive, when it was.</param>
    /// <returns>The process, or <see langword="null"/>.</returns>
    public Process2? FindLocalProcess(int? processId, string? processName, out string? problem) =>
        Find(Debugger.LocalProcesses, processId, processName, out problem);

    /// <summary>
    /// Finds a process this instance is debugging, by id or by exact executable name.
    /// </summary>
    /// <param name="processId">The process id, which wins when given.</param>
    /// <param name="processName">The executable name.</param>
    /// <param name="problem">Why the lookup was inconclusive, when it was.</param>
    /// <returns>The debugged process, or <see langword="null"/>.</returns>
    public Process2? FindDebuggedProcess(int? processId, string? processName, out string? problem) =>
        Find(Debugger.DebuggedProcesses, processId, processName, out problem);

    /// <summary>
    /// Reports whether this instance is debugging a process.
    /// </summary>
    /// <param name="processId">The process id.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    public bool IsDebugging(int processId) => Find(Debugger.DebuggedProcesses, processId, null, out _) is not null;

    /// <summary>
    /// Attaches this instance's debugger to one process with the given engine, or with Visual
    /// Studio's own detection when no engine is named.
    /// </summary>
    /// <param name="process">The local process.</param>
    /// <param name="engineName">The engine name, or <see langword="null"/>.</param>
    public static void Attach(Process2 process, string? engineName)
    {
        ArgumentNullException.ThrowIfNull(process);

        if (engineName is null)
        {
            // No .NET Core engine was listed: let Visual Studio pick, as the dialog's default would.
            process.Attach();
        }
        else
        {
            process.Attach2(engineName);
        }
    }

    /// <summary>
    /// Detaches this instance's debugger from one process and leaves it running.
    /// </summary>
    /// <param name="process">The debugged process.</param>
    public static void Detach(Process2 process)
    {
        ArgumentNullException.ThrowIfNull(process);

        process.Detach(WaitForBreakOrEnd: false);
    }

    private Debugger2 Debugger => (Debugger2)_dte.Debugger;

    private static Process2? Find(Processes processes, int? processId, string? processName, out string? problem)
    {
        problem = null;
        var matches = new List<Process2>();

        foreach (Process2 process in processes)
        {
            if (processId is { } id ? process.ProcessID == id : NameMatches(process, processName))
            {
                matches.Add(process);
            }
        }

        if (matches.Count > 1)
        {
            problem = $"{matches.Count} processes are named {processName} (PIDs {string.Join(", ", matches.Select(match => match.ProcessID))}); pass --process-id.";
            return null;
        }

        return matches.Count == 1 ? matches[0] : null;
    }

    private static bool NameMatches(Process2 process, string? processName) =>
        processName is not null
        && string.Equals(Path.GetFileName(process.Name), processName, StringComparison.OrdinalIgnoreCase);

    private static string ProductYear(int majorVersion) => majorVersion switch
    {
        18 => "2026",
        17 => "2022",
        16 => "2019",
        _ => $"{majorVersion}.0"
    };
}
