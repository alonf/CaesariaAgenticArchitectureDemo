namespace OperationsAgent.Api.Services.Capabilities;

/// <summary>
/// Case memory: the agent's own closed cases, recalled across sessions as hypotheses through a
/// custom <c>AIContextProvider</c>. Memory is never evidence - the provider labels what it recalls
/// as untrusted reference data, and this capability reports which cases it recalled.
/// </summary>
internal sealed class CaseMemoryCapability(ICaseMemoryStore caseMemoryStore, ILoggerFactory loggerFactory) : AgentCapability
{
    // Hypothesis trace for the UI: which closed cases the provider recalled this run.
    private readonly List<ClosedCase> _recalledCases = [];

    public override bool IsAvailable(DemoStage stage) => stage >= DemoStage.Memory;

    public override ValueTask ComposeAsync(AgentComposition composition, CancellationToken cancellationToken)
    {
        #region CASE_MEMORY
        DemoBreakpoints.Pause(DemoSnippets.CaseMemory);

        var caseMemory = new CaseMemoryProvider(
            caseMemoryStore,
            recalled => _recalledCases.AddRange(recalled),
            composition.CorrelationId,
            loggerFactory.CreateLogger<CaseMemoryProvider>());

        composition.ContextProviders.Add(caseMemory);
        #endregion

        return ValueTask.CompletedTask;
    }

    public override void Describe(AgentRunTrace trace, OperationsAgentAnswerParts parts) =>
        parts.RecalledCases.AddRange(_recalledCases
            .DistinctBy(item => item.CaseId, StringComparer.OrdinalIgnoreCase)
            .Select(item => new OperationsAgentRecalledCase(
                item.CaseId, item.AssetId, item.Symptom, item.Resolution, item.ClosedAt)));
}
