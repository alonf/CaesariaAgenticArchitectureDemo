using System.ComponentModel;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Exposes authoritative Energy Hub reads as ordinary C# function tools.
/// </summary>
public sealed partial class EnergyTools
{
    /// <summary>
    /// Gets the stable name of the single Stage 1 tool.
    /// </summary>
    public const string StreetlightStateToolName = "get_streetlight_state";

    private readonly IEnergyReadGateway _energyReadGateway;
    private readonly Func<string> _correlationId;
    private readonly ILogger<EnergyTools> _logger;

    /// <summary>Creates tools for one local invocation with its explicit correlation identifier.</summary>
    public EnergyTools(IEnergyReadGateway energyReadGateway, string correlationId, ILogger<EnergyTools> logger)
        : this(energyReadGateway, () => correlationId, logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
    }

    /// <summary>Creates reusable hosted tools that resolve correlation separately for each invocation.</summary>
    public EnergyTools(IEnergyReadGateway energyReadGateway, Func<string> correlationId, ILogger<EnergyTools> logger)
    {
        _energyReadGateway = energyReadGateway ?? throw new ArgumentNullException(nameof(energyReadGateway));
        _correlationId = correlationId ?? throw new ArgumentNullException(nameof(correlationId));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

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
        var correlationId = _correlationId();
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        EnergyToolsLog.ToolInvoked(_logger, StreetlightStateToolName, assetId, correlationId);

        return await _energyReadGateway.GetStateAsync(assetId, correlationId, cancellationToken);
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
