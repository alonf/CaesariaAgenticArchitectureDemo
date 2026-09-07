namespace CommandCenter.Web.Services;

/// <summary>
/// Describes, for the agent's case memory, what the authoritative snapshot showed when the operator
/// closed a case. Two things pull at the wording. It must be true of the snapshot: a canned anomaly
/// sentence would file "on during daylight against its schedule" for a lamp that is off, or for one
/// deliberately lit for an operation, and that would come back later as a recalled hypothesis that
/// never happened. And it must be said in the operator's words: the case store recalls by the
/// meaningful terms a question and a symptom share, so a symptom that states the same facts in a
/// different vocabulary than the question ("effective target" where the question says "daylight")
/// is never recalled at all, and the Memory beat shows nothing.
/// </summary>
internal static class CaseSymptom
{
    /// <summary>
    /// Describes the observed state as a one-sentence case symptom.
    /// </summary>
    /// <param name="assetId">The asset the case concerns.</param>
    /// <param name="state">The authoritative operational twin on screen when the case was closed.</param>
    /// <returns>The symptom, phrased the way the operator's questions are.</returns>
    public static string Describe(string assetId, EnergyOperationalTwin state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
        ArgumentNullException.ThrowIfNull(state);

        var observed = state.ReportedIsOn ? "on" : "off";
        var phase = state.IsDaylight ? "during daylight" : "after dark";

        if (!state.IsAnomalous)
        {
            return state.OperationContext.RequiresLighting
                ? $"Streetlight {assetId} {observed} {phase} as an active operational context required."
                : $"Streetlight {assetId} {observed} {phase}, matching its schedule.";
        }

        // Against the schedule when nothing outranks it; against the effective target when a
        // cross-domain requirement does, which is the only time the two differ.
        var reference = state.OperationContext.RequiresLighting ? "effective target" : "schedule";
        var overrideState = state.ManualOverride ? "under a manual override" : "with no manual override";

        return $"Streetlight {assetId} {observed} {phase} against its {reference}, {overrideState}.";
    }
}
