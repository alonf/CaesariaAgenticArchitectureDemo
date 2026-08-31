namespace OperationsAgent.Api.Services;

/// <summary>
/// Represents one piece of normalized organizational work evidence. Both the simulated provider and
/// the optional live Work IQ provider return this same shape, so the agent's grounding is identical
/// regardless of the evidence source.
/// </summary>
/// <param name="Id">The stable evidence identifier, for example a work-order number.</param>
/// <param name="SourceType">The evidence kind, for example "Work order" or "Technician note".</param>
/// <param name="Title">The evidence title.</param>
/// <param name="Summary">The evidence content relevant to operational reasoning.</param>
/// <param name="OccurredAt">When the evidence was produced.</param>
/// <param name="SourceLabel">The provenance label, for example "Simulated work knowledge".</param>
/// <param name="SourceUri">A link to the original item (email, task, document) when the provider
/// has one; the simulator leaves it <see langword="null"/>.</param>
public sealed record WorkEvidence(
    string Id,
    string SourceType,
    string Title,
    string Summary,
    DateTimeOffset OccurredAt,
    string SourceLabel,
    string? SourceUri = null);

/// <summary>
/// Searches organizational work knowledge for evidence relevant to an operational question.
/// </summary>
public interface IWorkKnowledgeSearch
{
    /// <summary>
    /// Searches the work knowledge store.
    /// </summary>
    /// <param name="query">The natural-language search query.</param>
    /// <param name="correlationId">The correlation identifier spanning the request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The matching evidence, newest first; empty when nothing matches.</returns>
    public Task<IReadOnlyList<WorkEvidence>> SearchAsync(string query, string correlationId, CancellationToken cancellationToken);
}

/// <summary>
/// Reports whether the simulated work-knowledge store currently holds evidence for the demo asset.
/// </summary>
/// <param name="EvidencePresent">Whether the seeded work evidence is available to searches.</param>
public sealed record WorkKnowledgeStatus(bool EvidencePresent);

/// <summary>
/// Deterministic in-memory work knowledge used by the lecture demo. The presenter can withhold the
/// seeded evidence to show that the agent reports missing evidence instead of inventing a ticket.
/// </summary>
public sealed partial class SimulatedWorkKnowledgeSearch(
    TimeProvider timeProvider,
    ILogger<SimulatedWorkKnowledgeSearch> logger) : IWorkKnowledgeSearch
{
    private static readonly string[] MatchTerms = ["l-417", "417", "streetlight", "street light", "light", "maintenance", "override", "lamp"];
    private volatile bool _evidencePresent = true;

    /// <summary>
    /// Gets or sets whether the seeded evidence is available to searches.
    /// </summary>
    public bool EvidencePresent
    {
        get => _evidencePresent;
        set => _evidencePresent = value;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkEvidence>> SearchAsync(string query, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        var matches = _evidencePresent && MatchTerms.Any(term => query.Contains(term, StringComparison.OrdinalIgnoreCase))
            ? CreateSeededEvidence()
            : [];

        WorkKnowledgeLog.Searched(logger, query, matches.Count, correlationId);
        return Task.FromResult(matches);
    }

    private IReadOnlyList<WorkEvidence> CreateSeededEvidence()
    {
        var now = timeProvider.GetUtcNow();

        return
        [
            new WorkEvidence(
                "WO-8732",
                "Work order",
                "Streetlight L-417 luminaire maintenance",
                "Scheduled luminaire maintenance on streetlight L-417 (North Promenade) completed this morning. " +
                "Manual override engaged during the work.",
                now.AddHours(-4),
                "Simulated work knowledge"),
            new WorkEvidence(
                "WO-8732/NOTE-1",
                "Technician note",
                "Post-maintenance verification for L-417",
                "Left the light ON for post-maintenance verification. Override to be cleared after the " +
                "afternoon inspection confirms the luminaire is stable.",
                now.AddHours(-3),
                "Simulated work knowledge")
        ];
    }
}

internal static partial class WorkKnowledgeLog
{
    [LoggerMessage(
        EventId = 2500,
        Level = LogLevel.Information,
        Message = "Work knowledge searched for \"{Query}\"; {MatchCount} match(es). CorrelationId: {CorrelationId}.")]
    internal static partial void Searched(ILogger logger, string query, int matchCount, string correlationId);
}
