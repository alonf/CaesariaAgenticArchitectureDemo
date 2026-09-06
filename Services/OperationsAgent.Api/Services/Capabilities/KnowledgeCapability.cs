using Microsoft.Agents.AI;

namespace OperationsAgent.Api.Services.Capabilities;

/// <summary>
/// Knowledge retrieval: an on-demand search tool over organizational work knowledge, joined as an
/// <c>AIContextProvider</c>. What it reports is what the search returned - not the same claim as
/// what the agent cited, which is the Command Center's badge to award.
/// </summary>
internal sealed class KnowledgeCapability(IWorkKnowledgeSearch workKnowledgeSearch, ILoggerFactory loggerFactory) : AgentCapability
{
    // Retrieval trace for the UI. Tool invocations run sequentially, so a plain list is safe.
    private readonly List<WorkEvidence> _retrievedEvidence = [];

    public override bool IsAvailable(DemoStage stage) => stage >= DemoStage.Knowledge;

    public override ValueTask ComposeAsync(AgentComposition composition, CancellationToken cancellationToken)
    {
        var correlationId = composition.CorrelationId;

        #region KNOWLEDGE_RETRIEVAL
        DemoBreakpoints.Pause(DemoSnippets.Knowledge);

        var workKnowledge = new TextSearchProvider(
            async (query, searchCancellationToken) =>
            {
                var evidence = await workKnowledgeSearch.SearchAsync(query, correlationId, searchCancellationToken);
                _retrievedEvidence.AddRange(evidence);
                return evidence.Select(item => new TextSearchProvider.TextSearchResult
                {
                    SourceName = $"{item.SourceType} {item.Id} ({item.SourceLabel})",
                    Text = $"{item.Title} - {item.Summary} (recorded {item.OccurredAt:u})"
                });
            },
            new TextSearchProviderOptions
            {
                SearchTime = TextSearchProviderOptions.TextSearchBehavior.OnDemandFunctionCalling,
                FunctionToolName = OperationsAgentToolNames.SearchWorkKnowledge,
                FunctionToolDescription =
                    "Searches organizational work knowledge such as work orders, technician notes, and maintenance records."
            },
            loggerFactory);

        composition.ContextProviders.Add(workKnowledge);
        #endregion

        return ValueTask.CompletedTask;
    }

    public override void Describe(AgentRunTrace trace, OperationsAgentAnswerParts parts) =>
        parts.Evidence.AddRange(_retrievedEvidence
            .DistinctBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Select(item => new OperationsAgentEvidence(
                item.Id, item.SourceType, item.Title, item.Summary, item.OccurredAt, item.SourceLabel, item.SourceUri)));
}
