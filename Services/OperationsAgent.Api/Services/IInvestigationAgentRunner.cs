namespace OperationsAgent.Api.Services;

/// <summary>
/// Runs the model reasoning step of a read-only investigation. Implementations decide, on the model's behalf,
/// which of the supplied toolset's read-only tools to invoke and in what order, then return the strict,
/// schema-validated model response. A fake implementation lets investigation orchestration be tested without a
/// live Microsoft Foundry connection.
/// </summary>
public interface IInvestigationAgentRunner
{
    /// <summary>
    /// Runs the investigation reasoning step for the supplied asset and returns the strict model-shaped response.
    /// </summary>
    /// <param name="toolset">The bound, read-only toolset available to the agent for this investigation.</param>
    /// <param name="assetId">The asset identifier under investigation.</param>
    /// <param name="question">The investigative question posed to the agent.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The strict, schema-validated model investigation response.</returns>
    public Task<ModelInvestigationResponse> InvestigateAsync(
        OperationsToolset toolset,
        string assetId,
        string question,
        CancellationToken cancellationToken);
}
