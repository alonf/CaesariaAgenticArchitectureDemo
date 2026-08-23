namespace EnergyHub.Api.Services;

/// <summary>
/// Provides correlated access to the vendor-facing SmartPole simulator boundary.
/// </summary>
public interface ISmartPoleGateway
{
    /// <summary>
    /// Reads the current authoritative SmartPole state for the supplied asset.
    /// </summary>
    /// <param name="assetId">The asset identifier to query.</param>
    /// <param name="correlationId">The correlation identifier spanning the end-to-end request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The current authoritative physical state.</returns>
    public Task<SmartPolePhysicalState> GetStateAsync(string assetId, string correlationId, CancellationToken cancellationToken);

    /// <summary>
    /// Submits a correlated lamp-state command to the SmartPole boundary.
    /// </summary>
    /// <param name="command">The command to send.</param>
    /// <param name="correlationId">The correlation identifier spanning the end-to-end request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The deterministic SmartPole command result.</returns>
    public Task<SmartPoleCommandResult> SetLampStateAsync(SetLampStateCommand command, string correlationId, CancellationToken cancellationToken);
}
