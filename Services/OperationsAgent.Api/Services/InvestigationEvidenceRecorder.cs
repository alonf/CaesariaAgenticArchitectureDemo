namespace OperationsAgent.Api.Services;

/// <summary>
/// Records the ordered read-only tool calls performed while investigating a single asset.
/// A new instance is created for each investigation request.
/// </summary>
/// <param name="timeProvider">The clock used to stamp each recorded tool call.</param>
public sealed class InvestigationEvidenceRecorder(TimeProvider timeProvider)
{
    private readonly object _gate = new();
    private readonly List<EvidenceTraceEntry> _entries = [];
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>
    /// Records a single read-only tool call outcome.
    /// </summary>
    /// <param name="toolName">The stable name of the invoked tool.</param>
    /// <param name="assetId">The asset identifier the tool call targeted.</param>
    /// <param name="outputSummary">The evidence summary returned by the tool.</param>
    /// <param name="succeeded">Indicates whether the tool call returned usable evidence.</param>
    /// <returns>The recorded evidence trace entry.</returns>
    public EvidenceTraceEntry Record(string toolName, string assetId, string outputSummary, bool succeeded)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputSummary);

        lock (_gate)
        {
            var entry = new EvidenceTraceEntry(_entries.Count + 1, toolName, assetId, outputSummary, _timeProvider.GetUtcNow(), succeeded);
            _entries.Add(entry);
            return entry;
        }
    }

    /// <summary>
    /// Gets the ordered evidence trace recorded so far.
    /// </summary>
    /// <returns>The recorded evidence trace, ordered from first to last tool call.</returns>
    public IReadOnlyList<EvidenceTraceEntry> GetTrace()
    {
        lock (_gate)
        {
            return _entries.ToArray();
        }
    }
}
