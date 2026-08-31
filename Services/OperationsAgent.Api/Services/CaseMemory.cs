using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Represents one investigation the operator closed: the agent's own past conclusion, kept so a
/// later, similar case can start from a hypothesis instead of from nothing. Memory is never
/// evidence - it records what the agent concluded once, not what is true now.
/// </summary>
/// <param name="CaseId">The stable case identifier, for example CASE-1.</param>
/// <param name="AssetId">The asset the case concerned.</param>
/// <param name="Symptom">The observed symptom that opened the case.</param>
/// <param name="Resolution">The closing conclusion, including the evidence it rested on.</param>
/// <param name="ClosedAt">When the operator closed the case.</param>
public sealed record ClosedCase(
    string CaseId,
    string AssetId,
    string Symptom,
    string Resolution,
    DateTimeOffset ClosedAt);

/// <summary>
/// Stores and recalls the agent's closed cases. The deterministic in-memory implementation is the
/// lecture default; the seam exists so an embedding-backed store can slot in without changing the
/// provider that consumes it.
/// </summary>
public interface ICaseMemoryStore
{
    /// <summary>
    /// Gets all closed cases, newest first.
    /// </summary>
    public IReadOnlyList<ClosedCase> GetAll();

    /// <summary>
    /// Records one closed case and returns it with its assigned identifier.
    /// </summary>
    /// <param name="assetId">The asset the case concerned.</param>
    /// <param name="symptom">The observed symptom that opened the case.</param>
    /// <param name="resolution">The closing conclusion.</param>
    public ClosedCase Record(string assetId, string symptom, string resolution);

    /// <summary>
    /// Recalls closed cases whose symptom resembles the question. Cross-asset recall is the point:
    /// a similar symptom on a different asset is exactly when past experience helps.
    /// </summary>
    /// <param name="question">The operator question to match against.</param>
    public IReadOnlyList<ClosedCase> Recall(string question);

    /// <summary>
    /// Removes all closed cases (presenter reset).
    /// </summary>
    public void Clear();
}

/// <summary>
/// Deterministic keyword-matched case memory. At demo scale (a handful of cases) semantic search
/// adds nothing observable; what matters is provenance - these are the agent's own conclusions.
/// </summary>
public sealed partial class InMemoryCaseMemoryStore(
    TimeProvider timeProvider,
    ILogger<InMemoryCaseMemoryStore> logger) : ICaseMemoryStore
{
    private const int MaxCases = 20;
    private static readonly string[] RecallTerms = ["streetlight", "street light", "light", "lamp", "daylight", "on", "override", "schedule"];
    private readonly object _gate = new();
    private readonly List<ClosedCase> _cases = [];
    private int _nextCaseNumber = 1;

    /// <inheritdoc />
    public IReadOnlyList<ClosedCase> GetAll()
    {
        lock (_gate)
        {
            return [.. _cases.OrderByDescending(closedCase => closedCase.ClosedAt)];
        }
    }

    /// <inheritdoc />
    public ClosedCase Record(string assetId, string symptom, string resolution)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(symptom);
        ArgumentException.ThrowIfNullOrWhiteSpace(resolution);

        lock (_gate)
        {
            var closedCase = new ClosedCase($"CASE-{_nextCaseNumber++}", assetId, symptom, resolution, timeProvider.GetUtcNow());
            _cases.Add(closedCase);

            while (_cases.Count > MaxCases)
            {
                _cases.RemoveAt(0);
            }

            CaseMemoryLog.CaseRecorded(logger, closedCase.CaseId, assetId);
            return closedCase;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ClosedCase> Recall(string question)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);

        lock (_gate)
        {
            return [.. _cases
                .Where(closedCase => RecallTerms.Any(term =>
                    question.Contains(term, StringComparison.OrdinalIgnoreCase)
                    && closedCase.Symptom.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(closedCase => closedCase.ClosedAt)];
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_gate)
        {
            _cases.Clear();
        }
    }
}

/// <summary>
/// Injects recalled closed cases into the agent invocation as explicitly framed hypotheses. The
/// instructions are transient (per invocation) and insist the agent verify live state and search
/// for real evidence before relying on any recalled conclusion: memory is a lead, not a fact.
/// </summary>
public sealed class CaseMemoryProvider(
    ICaseMemoryStore caseMemoryStore,
    Action<IReadOnlyList<ClosedCase>> onRecalled,
    string correlationId,
    ILogger<CaseMemoryProvider> logger) : AIContextProvider
{
    /// <inheritdoc />
    protected override ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        var question = context.AIContext.Messages?
            .LastOrDefault(message => message.Role == ChatRole.User)?.Text;

        if (string.IsNullOrWhiteSpace(question))
        {
            return ValueTask.FromResult(new AIContext());
        }

        var recalledCases = caseMemoryStore.Recall(question);
        onRecalled(recalledCases);
        CaseMemoryLog.CasesRecalled(logger, recalledCases.Count, correlationId);

        if (recalledCases.Count == 0)
        {
            return ValueTask.FromResult(new AIContext());
        }

        var recalledSummaries = string.Join(
            Environment.NewLine,
            recalledCases.Select(closedCase =>
                $"- {closedCase.CaseId} ({closedCase.AssetId}, closed {closedCase.ClosedAt:u}): {closedCase.Symptom} -> {closedCase.Resolution}"));

        return ValueTask.FromResult(new AIContext
        {
            Instructions = $"""
                You have memory of similar cases you closed earlier:
                {recalledSummaries}
                Treat recalled cases as hypotheses only, never as evidence about the current asset.
                Verify the current live state and search for real evidence before relying on them.
                When no direct evidence explains the current state, mention the most similar
                recalled case by its case identifier as a possible analogous explanation - clearly
                labeled as an unconfirmed hypothesis from a different asset, never as a confirmed
                cause.
                """
        });
    }
}

internal static partial class CaseMemoryLog
{
    [LoggerMessage(
        EventId = 2520,
        Level = LogLevel.Information,
        Message = "Closed case {CaseId} recorded for asset {AssetId}.")]
    internal static partial void CaseRecorded(ILogger logger, string caseId, string assetId);

    [LoggerMessage(
        EventId = 2521,
        Level = LogLevel.Information,
        Message = "Case memory recalled {RecalledCount} case(s). CorrelationId: {CorrelationId}.")]
    internal static partial void CasesRecalled(ILogger logger, int recalledCount, string correlationId);
}
