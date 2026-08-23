using System.ComponentModel;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Exposes the narrow, read-only evidence tools the Operations Agent can choose among while investigating
/// a single asset. Every method validates its bound context, propagates the investigation correlation identifier,
/// emits structured logs, and records an evidence-trace entry describing what was found. This toolset can never
/// write, restore, command, apply a scenario, or reach the vendor/device simulator layer directly.
/// </summary>
/// <param name="assetId">The asset identifier under investigation.</param>
/// <param name="correlationId">The correlation identifier spanning the investigation request.</param>
/// <param name="energyReadGateway">The read-only gateway used to reach the Energy Hub.</param>
/// <param name="commandCenterReadGateway">The read-only gateway used to reach the Command Center.</param>
/// <param name="evidenceRecorder">The recorder used to capture the ordered evidence trace.</param>
/// <param name="logger">The logger used for tool invocation events.</param>
public sealed partial class OperationsToolset(
    string assetId,
    string correlationId,
    IEnergyReadGateway energyReadGateway,
    ICommandCenterReadGateway commandCenterReadGateway,
    InvestigationEvidenceRecorder evidenceRecorder,
    ILogger<OperationsToolset> logger)
{
    private readonly string _assetId = string.IsNullOrWhiteSpace(assetId)
        ? throw new ArgumentException("An investigated asset identifier is required.", nameof(assetId))
        : assetId;
    private readonly string _correlationId = string.IsNullOrWhiteSpace(correlationId)
        ? throw new ArgumentException("An investigation correlation identifier is required.", nameof(correlationId))
        : correlationId;
    private readonly IEnergyReadGateway _energyReadGateway = energyReadGateway ?? throw new ArgumentNullException(nameof(energyReadGateway));
    private readonly ICommandCenterReadGateway _commandCenterReadGateway = commandCenterReadGateway ?? throw new ArgumentNullException(nameof(commandCenterReadGateway));
    private readonly InvestigationEvidenceRecorder _evidenceRecorder = evidenceRecorder ?? throw new ArgumentNullException(nameof(evidenceRecorder));
    private readonly ILogger<OperationsToolset> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Gets the stable tool name used for the customer-report evidence read.
    /// </summary>
    public const string CustomerReportToolName = "get_customer_report";

    /// <summary>
    /// Gets the stable tool name used for the Energy Hub asset-state evidence read.
    /// </summary>
    public const string EnergyAssetStateToolName = "get_energy_asset_state";

    /// <summary>
    /// Gets the stable tool name used for the Energy Hub recent-activity evidence read.
    /// </summary>
    public const string EnergyRecentActivityToolName = "get_energy_recent_activity";

    /// <summary>
    /// Gets the stable tool name used for the Command Center incident-context evidence read.
    /// </summary>
    public const string IncidentContextToolName = "get_incident_context";

    /// <summary>
    /// Reads the current customer-reported issue for the investigated streetlight asset, if any has been received.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the read.</param>
    /// <returns>A plain-text evidence summary describing the customer report, or its absence.</returns>
    [Description("Reads the current customer-reported issue for the investigated streetlight asset, if any has been received. Takes no arguments.")]
    public async Task<string> GetCustomerReportAsync(CancellationToken cancellationToken)
    {
        OperationsToolsetLog.ToolInvoked(_logger, CustomerReportToolName, _assetId, _correlationId);

        try
        {
            var context = await _commandCenterReadGateway.GetCustomerReportContextAsync(_assetId, _correlationId, cancellationToken);
            var summary = context.Report is null
                ? $"No customer report is currently on file for asset {_assetId}."
                : $"Customer report {context.Report.Id} received at {context.Report.ReceivedAt:O} via {context.Report.Source}: \"{context.Report.Message}\"";

            _evidenceRecorder.Record(CustomerReportToolName, _assetId, summary, succeeded: true);
            return summary;
        }
        catch (HttpRequestException exception)
        {
            return RecordFailure(CustomerReportToolName, "The customer report source is currently unavailable.", exception);
        }
        catch (InvalidOperationException exception)
        {
            return RecordFailure(CustomerReportToolName, "The customer report source returned an invalid response.", exception);
        }
    }

    /// <summary>
    /// Reads the authoritative Energy Hub operational state for the investigated streetlight asset.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the read.</param>
    /// <returns>A plain-text evidence summary describing the authoritative asset state.</returns>
    [Description("Reads the authoritative Energy Hub operational state for the investigated streetlight asset. Takes no arguments.")]
    public async Task<string> GetEnergyAssetStateAsync(CancellationToken cancellationToken)
    {
        OperationsToolsetLog.ToolInvoked(_logger, EnergyAssetStateToolName, _assetId, _correlationId);

        try
        {
            var state = await _energyReadGateway.GetStateAsync(_assetId, _correlationId, cancellationToken);
            var summary =
                $"Asset {state.AssetId} in {state.Area}: ReportedIsOn={state.ReportedIsOn}, DesiredIsOn={state.DesiredIsOn}, " +
                $"IsDaylight={state.IsDaylight}, ExpectedScheduledState={state.ExpectedScheduledState}, ManualOverride={state.ManualOverride}, " +
                $"ControllerHealth={state.ControllerHealth.Status} ({state.ControllerHealth.Summary}), " +
                $"OpenIncidentId={state.OpenIncidentId ?? "none"}, LastMaintenanceTime={FormatTimestamp(state.LastMaintenanceTime)}, " +
                $"OperationContext={state.OperationContext.Summary}, LastReportedAt={state.LastReportedAt:O}.";

            _evidenceRecorder.Record(EnergyAssetStateToolName, _assetId, summary, succeeded: true);
            return summary;
        }
        catch (HttpRequestException exception)
        {
            return RecordFailure(EnergyAssetStateToolName, "The Energy Hub asset state is currently unavailable.", exception);
        }
        catch (InvalidOperationException exception)
        {
            return RecordFailure(EnergyAssetStateToolName, "The Energy Hub asset state returned an invalid response.", exception);
        }
    }

    /// <summary>
    /// Reads recent authoritative Energy Hub activity for the investigated streetlight asset.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the read.</param>
    /// <returns>A plain-text evidence summary describing recent Energy Hub activity.</returns>
    [Description("Reads recent authoritative Energy Hub activity for the investigated streetlight asset. Takes no arguments.")]
    public async Task<string> GetEnergyRecentActivityAsync(CancellationToken cancellationToken)
    {
        OperationsToolsetLog.ToolInvoked(_logger, EnergyRecentActivityToolName, _assetId, _correlationId);

        try
        {
            var activity = await _energyReadGateway.GetRecentActivityAsync(_assetId, 10, _correlationId, cancellationToken);
            var summary = activity.Count == 0
                ? $"No recent Energy Hub activity is recorded for asset {_assetId}."
                : string.Join(
                    " | ",
                    activity.Select(record => $"[{record.OccurredAt:O}] {record.Kind} ({record.Source}): {record.Message}"));

            _evidenceRecorder.Record(EnergyRecentActivityToolName, _assetId, summary, succeeded: true);
            return summary;
        }
        catch (HttpRequestException exception)
        {
            return RecordFailure(EnergyRecentActivityToolName, "The Energy Hub recent activity is currently unavailable.", exception);
        }
        catch (InvalidOperationException exception)
        {
            return RecordFailure(EnergyRecentActivityToolName, "The Energy Hub recent activity returned an invalid response.", exception);
        }
    }

    /// <summary>
    /// Reads the current open Command Center incident context for the investigated streetlight asset, if any exists.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the read.</param>
    /// <returns>A plain-text evidence summary describing the current incident context.</returns>
    [Description("Reads the current open Command Center incident context for the investigated streetlight asset, if any exists. Takes no arguments.")]
    public async Task<string> GetIncidentContextAsync(CancellationToken cancellationToken)
    {
        OperationsToolsetLog.ToolInvoked(_logger, IncidentContextToolName, _assetId, _correlationId);

        try
        {
            var context = await _commandCenterReadGateway.GetIncidentContextAsync(_assetId, _correlationId, cancellationToken);
            var summary = context.Incident is null
                ? $"No open incident is currently tracked for asset {_assetId}."
                : $"Incident {context.Incident.Id} ({context.Incident.Severity}, {context.Incident.Status}) opened at {context.Incident.CreatedAt:O}: {context.Incident.Title} - {context.Incident.Description}";

            _evidenceRecorder.Record(IncidentContextToolName, _assetId, summary, succeeded: true);
            return summary;
        }
        catch (HttpRequestException exception)
        {
            return RecordFailure(IncidentContextToolName, "The Command Center incident context is currently unavailable.", exception);
        }
        catch (InvalidOperationException exception)
        {
            return RecordFailure(IncidentContextToolName, "The Command Center incident context returned an invalid response.", exception);
        }
    }

    private string RecordFailure(string toolName, string failureSummary, Exception exception)
    {
        OperationsToolsetLog.ToolFailed(_logger, toolName, _assetId, _correlationId, exception);
        _evidenceRecorder.Record(toolName, _assetId, failureSummary, succeeded: false);
        return failureSummary;
    }

    private static string FormatTimestamp(DateTimeOffset? value) => value is null ? "unknown" : value.Value.ToString("O");
}

internal static partial class OperationsToolsetLog
{
    [LoggerMessage(
        EventId = 2300,
        Level = LogLevel.Information,
        Message = "Operations Agent invoked tool {ToolName} for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void ToolInvoked(ILogger logger, string toolName, string assetId, string correlationId);

    [LoggerMessage(
        EventId = 2301,
        Level = LogLevel.Warning,
        Message = "Operations Agent tool {ToolName} for asset {AssetId} could not obtain evidence. CorrelationId: {CorrelationId}.")]
    internal static partial void ToolFailed(ILogger logger, string toolName, string assetId, string correlationId, Exception exception);
}
