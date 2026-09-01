namespace CommandCenter.Web.Services;

/// <summary>
/// Decides whether an agent answer still describes the operational state on screen. An answer is
/// produced against the evidence version captured when the question was asked; if the state
/// changed underneath it, the answer is stale - unless the change is the agent's own approved
/// write, recognized by the last command carrying the answer's correlation identifier while every
/// other piece of evidence is untouched.
/// </summary>
public static class AgentEvidenceGuard
{
    // The first four version components are ambient evidence - stage, scenario, customer report
    // and open incident. Only the last two - the telemetry timestamp and the last command - may
    // legitimately move under the agent's own approved write.
    private const int AmbientComponentCount = 4;
    private const int ComponentCount = 6;

    /// <summary>
    /// Computes the evidence version of a snapshot: the correlation identifiers and timestamps
    /// that change whenever the operational picture the agent reasons over changes.
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
                snapshot.OperationalState.LastReportedAt.ToString("O"),
                snapshot.OperationalState.LastCommand?.CorrelationId);

    /// <summary>
    /// Determines whether an answer produced against <paramref name="requestedEvidenceVersion"/>
    /// is still current for <paramref name="currentSnapshot"/>. When it is, the caller should
    /// adopt <paramref name="currentEvidenceVersion"/> as the answer's version, so an own write
    /// advances the guard instead of tripping it on the next refresh.
    /// </summary>
    /// <param name="requestedEvidenceVersion">The evidence version the answer is valid against.</param>
    /// <param name="currentSnapshot">The snapshot on screen now.</param>
    /// <param name="answerCorrelationId">The correlation identifier of the agent run that produced the answer.</param>
    /// <param name="currentEvidenceVersion">The evidence version of <paramref name="currentSnapshot"/>.</param>
    /// <returns><see langword="true"/> when the answer may stay on screen.</returns>
    public static bool IsAnswerCurrent(
        string? requestedEvidenceVersion,
        CommandCenterSnapshot? currentSnapshot,
        string? answerCorrelationId,
        out string? currentEvidenceVersion)
    {
        currentEvidenceVersion = GetEvidenceVersion(currentSnapshot);

        if (string.Equals(requestedEvidenceVersion, currentEvidenceVersion, StringComparison.Ordinal))
        {
            return true;
        }

        if (string.IsNullOrEmpty(answerCorrelationId)
            || !string.Equals(
                currentSnapshot?.OperationalState.LastCommand?.CorrelationId,
                answerCorrelationId,
                StringComparison.Ordinal))
        {
            return false;
        }

        // The last command is this very run's approved restore. The answer stays current only if
        // that write is the whole difference: the ambient evidence must be untouched.
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

        return true;
    }
}
