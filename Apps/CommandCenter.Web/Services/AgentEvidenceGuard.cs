namespace CommandCenter.Web.Services;

/// <summary>
/// Decides whether an agent answer still describes the operational state on screen. An answer is
/// produced against the evidence version captured when the question was asked; if the state moved
/// underneath it, the answer is stale.
/// <para>
/// One exemption exists, and it is deliberately narrow: the agent's own approved write. That write
/// is the answer's own doing, so it may be adopted <em>once</em> - and only when the last command
/// is this run's, it succeeded, the observed state actually matches what that command asked for,
/// and nothing else changed. Every later change, including a lamp that switches back on by itself,
/// makes the answer stale again.
/// </para>
/// </summary>
public static class AgentEvidenceGuard
{
    // The first four version components are ambient evidence - stage, scenario, customer report
    // and open incident. The last is the authoritative state revision, which the Energy Hub bumps
    // on every mutation it accepts.
    private const int AmbientComponentCount = 4;
    private const int ComponentCount = 5;

    // Component order in the version string, so a difference can be named rather than guessed at.
    private static readonly AgentEvidenceChange[] ComponentChanges =
    [
        AgentEvidenceChange.Stage,
        AgentEvidenceChange.Scenario,
        AgentEvidenceChange.CustomerReport,
        AgentEvidenceChange.Incident,
        AgentEvidenceChange.OperationalState
    ];

    /// <summary>
    /// Computes the evidence version of a snapshot: the correlation identifiers and the
    /// authoritative state revision that change whenever the picture the agent reasons over
    /// changes.
    /// </summary>
    /// <param name="snapshot">The snapshot to fingerprint.</param>
    /// <returns>The opaque version string, or <see langword="null"/> without a snapshot.</returns>
    public static string? GetEvidenceVersion(CommandCenterSnapshot? snapshot) =>
        snapshot is null
            ? null
            : string.Join(
                '|',
                snapshot.CurrentStage.CorrelationId,
                snapshot.CurrentScenario.CorrelationId,
                snapshot.CustomerReport?.CorrelationId,
                snapshot.OpenIncident?.CorrelationId,
                snapshot.OperationalState.StateRevision.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>
    /// Determines whether an answer produced against <paramref name="requestedEvidenceVersion"/>
    /// is still current for <paramref name="currentSnapshot"/>.
    /// </summary>
    /// <param name="requestedEvidenceVersion">The evidence version the answer is valid against.</param>
    /// <param name="currentSnapshot">The snapshot on screen now.</param>
    /// <param name="answerCorrelationId">The correlation identifier of the agent run that produced the answer.</param>
    /// <param name="ownWriteAlreadyAdopted">
    /// Whether this answer has already absorbed its own write. Adoption happens at most once, so a
    /// second, unrelated change cannot hide behind the same command record.
    /// </param>
    /// <param name="currentEvidenceVersion">The evidence version of <paramref name="currentSnapshot"/>.</param>
    /// <param name="adoptedOwnWrite">Whether this call adopted the answer's own write.</param>
    /// <returns><see langword="true"/> when the answer may stay on screen.</returns>
    public static bool IsAnswerCurrent(
        string? requestedEvidenceVersion,
        CommandCenterSnapshot? currentSnapshot,
        string? answerCorrelationId,
        bool ownWriteAlreadyAdopted,
        out string? currentEvidenceVersion,
        out bool adoptedOwnWrite)
    {
        currentEvidenceVersion = GetEvidenceVersion(currentSnapshot);
        adoptedOwnWrite = false;

        if (string.Equals(requestedEvidenceVersion, currentEvidenceVersion, StringComparison.Ordinal))
        {
            return true;
        }

        if (ownWriteAlreadyAdopted || string.IsNullOrEmpty(answerCorrelationId) || currentSnapshot is null)
        {
            return false;
        }

        // The change may be adopted only if it is this run's own successful command...
        if (currentSnapshot.OperationalState.LastCommand is not { } lastCommand
            || !string.Equals(lastCommand.CorrelationId, answerCorrelationId, StringComparison.Ordinal)
            || lastCommand.Status != CommandExecutionStatus.Succeeded)
        {
            return false;
        }

        // ...the state on screen must be what that command asked for - an answer describing a
        // lamp that is off cannot survive a report that it is on...
        if (currentSnapshot.OperationalState.ReportedIsOn != lastCommand.DesiredIsOn)
        {
            return false;
        }

        // ...and the write must be the whole difference: ambient evidence untouched.
        var requestedComponents = requestedEvidenceVersion?.Split('|');
        var currentComponents = currentEvidenceVersion?.Split('|');

        if (requestedComponents is not { Length: ComponentCount } || currentComponents is not { Length: ComponentCount })
        {
            return false;
        }

        for (var i = 0; i < AmbientComponentCount; i++)
        {
            if (!string.Equals(requestedComponents[i], currentComponents[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        adoptedOwnWrite = true;
        return true;
    }

    /// <summary>
    /// Names what moved between two evidence versions, so the operator is told the actual cause -
    /// advancing the demo stage is not an operational change, and saying it was sends the presenter
    /// looking at the city for something that did not happen.
    /// </summary>
    /// <param name="requestedEvidenceVersion">The evidence version the answer is valid against.</param>
    /// <param name="currentEvidenceVersion">The evidence version on screen now.</param>
    /// <returns>The first component that differs, in fingerprint order.</returns>
    public static AgentEvidenceChange DescribeChange(string? requestedEvidenceVersion, string? currentEvidenceVersion)
    {
        if (string.Equals(requestedEvidenceVersion, currentEvidenceVersion, StringComparison.Ordinal))
        {
            return AgentEvidenceChange.None;
        }

        var requestedComponents = requestedEvidenceVersion?.Split('|');
        var currentComponents = currentEvidenceVersion?.Split('|');

        if (requestedComponents is not { Length: ComponentCount } || currentComponents is not { Length: ComponentCount })
        {
            return AgentEvidenceChange.Unknown;
        }

        for (var i = 0; i < ComponentCount; i++)
        {
            if (!string.Equals(requestedComponents[i], currentComponents[i], StringComparison.Ordinal))
            {
                return ComponentChanges[i];
            }
        }

        return AgentEvidenceChange.Unknown;
    }
}

/// <summary>
/// What made an agent answer stale. Several different events invalidate an answer, and they are
/// not equally alarming: a presenter advancing the stage is expected, a lamp changing underneath
/// the answer is the thing the operator needs to look at.
/// </summary>
public enum AgentEvidenceChange
{
    /// <summary>Nothing changed; the answer still describes the picture on screen.</summary>
    None,

    /// <summary>The presenter moved the demo stage.</summary>
    Stage,

    /// <summary>A different deterministic scenario was applied.</summary>
    Scenario,

    /// <summary>A customer report was raised or resolved.</summary>
    CustomerReport,

    /// <summary>An incident was opened or closed.</summary>
    Incident,

    /// <summary>The authoritative operational state moved.</summary>
    OperationalState,

    /// <summary>The versions differ but cannot be compared component by component.</summary>
    Unknown
}
