namespace CommandCenter.Api.Services;

/// <summary>
/// Provides correlated access to the Energy Hub boundary used by the Command Center.
/// </summary>
public interface IEnergyHubGateway
{
    /// <summary>
    /// Reads the current authoritative Energy Hub state for the supplied asset.
    /// </summary>
    /// <param name="assetId">The asset identifier to query.</param>
    /// <param name="correlationId">The correlation identifier spanning the end-to-end request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The authoritative operational twin.</returns>
    public Task<EnergyOperationalTwin> GetStateAsync(string assetId, string correlationId, CancellationToken cancellationToken);

    /// <summary>
    /// Reads recent authoritative Energy Hub activity for the supplied asset.
    /// </summary>
    /// <param name="assetId">The asset identifier to query.</param>
    /// <param name="limit">The maximum number of activity records to return.</param>
    /// <param name="correlationId">The correlation identifier spanning the end-to-end request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The recent activity ordered from newest to oldest.</returns>
    public Task<IReadOnlyList<ActivityRecord>> GetRecentActivityAsync(string assetId, int limit, string correlationId, CancellationToken cancellationToken);

    /// <summary>
    /// Requests the narrow deterministic Energy Hub operation that restores the asset to scheduled mode.
    /// </summary>
    /// <param name="assetId">The asset identifier to command.</param>
    /// <param name="correlationId">The correlation identifier spanning the end-to-end request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The authoritative Energy Hub command result.</returns>
    public Task<RestoreScheduledModeResult> RestoreScheduledModeAsync(string assetId, string correlationId, CancellationToken cancellationToken);
}
