namespace VisualStudioDemoAttach;

/// <summary>
/// Picks the Visual Studio instance to drive. Never "the first one running": the presenter may
/// have Visual Studio 2022 open on another repository, or a second 2026 window on a scratch
/// project, and the debugger must land in the window that has the Caesarea solution open. Once an
/// instance has attached, its process id pins later operations to that same window.
/// </summary>
internal static class InstanceSelection
{
    /// <summary>
    /// The oldest major version the demo attaches through: 18, Visual Studio 2026.
    /// </summary>
    public const int MinimumMajorVersion = 18;

    /// <summary>
    /// Chooses the instance for a solution.
    /// </summary>
    /// <param name="candidates">Every running instance.</param>
    /// <param name="solutionPath">The solution the instance must have open, or <see langword="null"/> to accept any single supported instance.</param>
    /// <param name="instanceProcessId">The devenv process id an earlier attach used, which must be the one used now, or <see langword="null"/>.</param>
    /// <param name="explanation">Why this instance, or why none.</param>
    /// <returns>The chosen instance, or <see langword="null"/> when none qualifies or several do.</returns>
    public static InstanceCandidate? Choose(IReadOnlyList<InstanceCandidate> candidates, string? solutionPath, int? instanceProcessId, out string explanation)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        if (instanceProcessId is { } pinned)
        {
            var instance = candidates.FirstOrDefault(candidate => candidate.ProcessId == pinned);

            if (instance is null)
            {
                explanation = $"The Visual Studio instance that attached (PID {pinned}) is no longer running.";
                return null;
            }

            explanation = $"{Describe(instance)} is the instance that attached.";
            return instance;
        }

        var supported = candidates.Where(candidate => candidate.MajorVersion >= MinimumMajorVersion).ToList();

        if (solutionPath is null)
        {
            return Single(supported, "supported instance", out explanation) ?? Fail(NoneSupported(candidates, supported), out explanation);
        }

        // The exact solution first; another solution file in the same folder only when nothing
        // has the exact one open. Several exact matches are a question for the presenter, not a
        // coin toss: attaching to one window while the other is the one they are looking at is
        // the failure this rule prevents.
        var exact = supported.Where(candidate => SolutionMatches(candidate.SolutionPath, solutionPath, sameFolderCounts: false)).ToList();

        if (exact.Count > 0)
        {
            return Single(exact, $"instance with {Path.GetFileName(solutionPath)} open", out explanation);
        }

        var sameFolder = supported.Where(candidate => SolutionMatches(candidate.SolutionPath, solutionPath, sameFolderCounts: true)).ToList();

        if (sameFolder.Count > 0)
        {
            return Single(sameFolder, $"instance with a solution from {Path.GetDirectoryName(solutionPath)} open", out explanation);
        }

        return Fail(NoneMatch(candidates, supported, solutionPath), out explanation);
    }

    /// <summary>
    /// Decides whether an instance's open solution is the wanted one.
    /// </summary>
    /// <param name="instanceSolution">The instance's open solution, or <see langword="null"/> when none is open.</param>
    /// <param name="wanted">The solution the caller wants.</param>
    /// <param name="sameFolderCounts">Whether another solution file in the same folder, which still opens this checkout's projects, also matches.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    public static bool SolutionMatches(string? instanceSolution, string wanted, bool sameFolderCounts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wanted);

        if (string.IsNullOrWhiteSpace(instanceSolution))
        {
            return false;
        }

        var open = Normalize(instanceSolution);
        var expected = Normalize(wanted);

        if (string.Equals(open, expected, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return sameFolderCounts
            && string.Equals(Path.GetDirectoryName(open), Path.GetDirectoryName(expected), StringComparison.OrdinalIgnoreCase);
    }

    private static InstanceCandidate? Single(List<InstanceCandidate> matches, string what, out string explanation)
    {
        switch (matches.Count)
        {
            case 1:
                explanation = $"{Describe(matches[0])} is the {what}.";
                return matches[0];
            case 0:
                explanation = $"No {what} is running.";
                return null;
            default:
                explanation = $"{matches.Count} instances qualify ({string.Join("; ", matches.Select(Describe))}). Close the extra Visual Studio window, or pass --instance-pid.";
                return null;
        }
    }

    private static InstanceCandidate? Fail(string reason, out string explanation)
    {
        explanation = reason;
        return null;
    }

    private static string Normalize(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static string Describe(InstanceCandidate candidate) =>
        $"{candidate.DisplayName} (PID {candidate.ProcessId}, {(candidate.SolutionPath is null ? "no solution" : Path.GetFileName(candidate.SolutionPath))})";

    private static string NoneSupported(IReadOnlyList<InstanceCandidate> candidates, List<InstanceCandidate> supported)
    {
        if (supported.Count > 1)
        {
            return $"Several supported instances are running ({string.Join("; ", supported.Select(Describe))}). Pass --solution to pick one.";
        }

        return candidates.Count == 0
            ? "Visual Studio is not running. Open the Caesarea solution in Visual Studio 2026 first."
            : $"No Visual Studio 2026 instance is running; found {string.Join("; ", candidates.Select(Describe))}. The demo attaches through Visual Studio 2026 only.";
    }

    private static string NoneMatch(IReadOnlyList<InstanceCandidate> candidates, List<InstanceCandidate> supported, string solutionPath)
    {
        var solutionName = Path.GetFileName(solutionPath);

        if (supported.Count == 0)
        {
            var older = candidates.Where(candidate => SolutionMatches(candidate.SolutionPath, solutionPath, sameFolderCounts: true)).ToList();

            return older.Count > 0
                ? $"{string.Join("; ", older.Select(Describe))} has {solutionName} open, but the demo attaches through Visual Studio 2026 only."
                : NoneSupported(candidates, supported);
        }

        return $"Visual Studio 2026 is running ({string.Join("; ", supported.Select(Describe))}), but no instance has {solutionName} open.";
    }
}

/// <summary>
/// A running instance reduced to what selection needs, so the choice is testable without COM.
/// </summary>
/// <param name="MajorVersion">The major version, 18 for Visual Studio 2026.</param>
/// <param name="ProcessId">The devenv process id.</param>
/// <param name="SolutionPath">The open solution's full path, or <see langword="null"/>.</param>
/// <param name="DisplayName">The product name for messages.</param>
internal sealed record InstanceCandidate(int MajorVersion, int ProcessId, string? SolutionPath, string DisplayName);
