using System.ComponentModel;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Exposes authoritative Energy Hub reads as ordinary C# function tools.
/// </summary>
public sealed partial class EnergyTools(
    IEnergyReadGateway energyReadGateway,
    string correlationId,
    ILogger<EnergyTools> logger)
{
    /// <summary>
    /// Gets the stable name of the single Stage 1 tool.
    /// </summary>
    public const string StreetlightStateToolName = "get_streetlight_state";

    private readonly IEnergyReadGateway _energyReadGateway =
        energyReadGateway ?? throw new ArgumentNullException(nameof(energyReadGateway));
    private readonly string _correlationId = string.IsNullOrWhiteSpace(correlationId)
        ? throw new ArgumentException("A correlation identifier is required.", nameof(correlationId))
        : correlationId;
    private readonly ILogger<EnergyTools> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    #region FUNCTION_TOOL
    /// <summary>
    /// Gets the current authoritative operational state of a streetlight.
    /// </summary>
    [Description("Gets the current authoritative operational state of a streetlight.")]
    public async Task<EnergyOperationalTwin> GetStreetlightStateAsync(
        [Description("The streetlight asset identifier, for example L-417.")]
        string assetId,
        CancellationToken cancellationToken)
    {
        DemoBreakpoints.Pause(DemoSnippets.FunctionTool);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
        EnergyToolsLog.ToolInvoked(_logger, StreetlightStateToolName, assetId, _correlationId);

        return await _energyReadGateway.GetStateAsync(assetId, _correlationId, cancellationToken);
    }
    #endregion
}

internal static partial class EnergyToolsLog
{
    [LoggerMessage(
        EventId = 2300,
        Level = LogLevel.Information,
        Message = "Operations Agent invoked tool {ToolName} for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void ToolInvoked(
        ILogger logger,
        string toolName,
        string assetId,
        string correlationId);
}
