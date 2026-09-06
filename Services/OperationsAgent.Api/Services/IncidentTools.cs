using System.ComponentModel;
using System.Net;
using System.Text.Json;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Reads incidents from the Command Center, the city's record of what it already knows about an
/// asset. Read-only on purpose: the agent may discover existing work here, never raise it.
/// </summary>
public interface IIncidentGateway
{
    /// <summary>
    /// Gets an incident by identifier.
    /// </summary>
    /// <returns>The incident, or <see langword="null"/> when the Command Center does not know it.</returns>
    public Task<IncidentRecord?> GetAsync(string incidentId, string correlationId, CancellationToken cancellationToken);
}

/// <summary>
/// The HTTP gateway to the Command Center's incident endpoint.
/// </summary>
public sealed partial class CommandCenterIncidentGateway(HttpClient httpClient, ILogger<CommandCenterIncidentGateway> logger) : IIncidentGateway
{
    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    /// <inheritdoc />
    public async Task<IncidentRecord?> GetAsync(string incidentId, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(incidentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/command-center/incidents/{Uri.EscapeDataString(incidentId)}");
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, correlationId);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        // An unknown identifier is an answer, not a failure: the model may have read one from a
        // stale note, and "no such incident" is what it needs to hear.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            CommandCenterIncidentGatewayLog.LookupFailed(logger, incidentId, correlationId, (int)response.StatusCode);
            throw new HttpRequestException(
                $"Command Center incident lookup failed with status code {(int)response.StatusCode}.", null, response.StatusCode);
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<IncidentRecord>(SerializerOptions, cancellationToken)
                ?? throw new InvalidOperationException("Command Center incident response was empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Command Center incident response payload was invalid.", exception);
        }
    }
}

/// <summary>
/// The read-only partner of the maintenance capability: before the agent files new work, it can
/// find out what the city already tracks. The asset's state names its open incident; this tool
/// says what that incident covers, so work that is already under way is not filed twice.
/// </summary>
public sealed partial class IncidentTools(
    IIncidentGateway incidents,
    string correlationId,
    ILogger<IncidentTools> logger)
{
    private const int MaxIncidentIdLength = 64;

    private readonly IIncidentGateway _incidents = incidents ?? throw new ArgumentNullException(nameof(incidents));
    private readonly string _correlationId = string.IsNullOrWhiteSpace(correlationId)
        ? throw new ArgumentException("A correlation identifier is required.", nameof(correlationId))
        : correlationId;
    private readonly ILogger<IncidentTools> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Describes an incident the Command Center tracks, or reports that it knows none by that identifier.
    /// </summary>
    [Description("Gets an incident the Command Center already tracks, by its identifier. Use it before filing new work: an asset's state names its open incident, and work that is already under way must not be filed twice.")]
    public async Task<string> GetIncidentAsync(
        [Description("The incident identifier, for example INC-L417-001.")] string incidentId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(incidentId);

        // The model supplies the identifier, so it is bounded before it travels: a runaway string
        // is answered, not forwarded to the Command Center.
        var trimmedId = incidentId.Trim();

        if (trimmedId.Length > MaxIncidentIdLength)
        {
            return "That is not a Caesarea incident identifier; incident identifiers look like INC-L417-001.";
        }

        IncidentToolsLog.LookupInvoked(_logger, trimmedId, _correlationId);

        var incident = await _incidents.GetAsync(trimmedId, _correlationId, cancellationToken);

        if (incident is null)
        {
            return $"The Command Center has no incident {trimmedId}.";
        }

        return $"Incident {incident.Id} ({incident.Status}, severity {incident.Severity}) tracks {incident.AssetId} in {incident.Area}: "
            + $"{incident.Title}. {incident.Description} Opened {incident.CreatedAt:u}.";
    }
}

internal static partial class IncidentToolsLog
{
    [LoggerMessage(
        EventId = 2670,
        Level = LogLevel.Information,
        Message = "Operations Agent looked up incident {IncidentId}. CorrelationId: {CorrelationId}.")]
    internal static partial void LookupInvoked(ILogger logger, string incidentId, string correlationId);
}

internal static partial class CommandCenterIncidentGatewayLog
{
    [LoggerMessage(
        EventId = 2671,
        Level = LogLevel.Warning,
        Message = "Command Center incident lookup for {IncidentId} failed with status {StatusCode}. CorrelationId: {CorrelationId}.")]
    internal static partial void LookupFailed(ILogger logger, string incidentId, string correlationId, int statusCode);
}
