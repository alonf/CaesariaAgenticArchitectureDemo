using System.Text.Json;

namespace DemoScenario.Api.Services;

/// <summary>
/// Reads and sets the Operations Agent's presenter switches in the canonical
/// <see cref="DemoSwitchValues"/> vocabulary, whatever shape each endpoint speaks.
/// </summary>
public interface IOperationsAgentSwitchClient
{
    /// <summary>Reads a switch's current value in the shared vocabulary.</summary>
    public Task<string> GetAsync(DemoSwitch demoSwitch, string correlationId, CancellationToken cancellationToken);

    /// <summary>Sets a switch to a value from the shared vocabulary; the agent may refuse it below the switch's stage.</summary>
    public Task SetAsync(DemoSwitch demoSwitch, string value, string correlationId, CancellationToken cancellationToken);
}

/// <summary>
/// The HTTP client for the agent's four switch endpoints. Each endpoint has its own small payload;
/// the translation to and from the shared vocabulary lives here and nowhere else.
/// </summary>
public sealed partial class HttpOperationsAgentSwitchClient(HttpClient httpClient, ILogger<HttpOperationsAgentSwitchClient> logger) : IOperationsAgentSwitchClient
{
    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    /// <inheritdoc />
    public async Task<string> GetAsync(DemoSwitch demoSwitch, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        using var request = CreateRequest(HttpMethod.Get, Route(demoSwitch), correlationId);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(SerializerOptions, cancellationToken);
        return Read(demoSwitch, payload);
    }

    /// <inheritdoc />
    public async Task SetAsync(DemoSwitch demoSwitch, string value, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        using var request = CreateRequest(HttpMethod.Post, Route(demoSwitch), correlationId);
        request.Content = JsonContent.Create(Write(demoSwitch, value), options: SerializerOptions);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        // The agent refuses a switch below its stage with a problem document; its detail is the
        // sentence the presenter needs to see.
        string? detail = null;

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(SerializerOptions, cancellationToken);

            if (problem.ValueKind == JsonValueKind.Object && problem.TryGetProperty("detail", out var detailProperty))
            {
                detail = detailProperty.GetString();
            }
        }
        catch (JsonException)
        {
            // A non-JSON body carries no better sentence than the status code.
        }

        SwitchClientLog.SetFailed(logger, demoSwitch, (int)response.StatusCode, correlationId);
        throw new HttpRequestException(
            detail ?? $"The Operations Agent refused the {DemoSwitchValues.Describe(demoSwitch)} switch with status code {(int)response.StatusCode}.",
            null,
            response.StatusCode);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string route, string correlationId)
    {
        var request = new HttpRequestMessage(method, route);
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, correlationId);
        return request;
    }

    private static string Route(DemoSwitch demoSwitch) => demoSwitch switch
    {
        DemoSwitch.ToolSource => "/api/operations-agent/tool-source/",
        DemoSwitch.SecurityConsult => "/api/operations-agent/security-consult/",
        DemoSwitch.WorkKnowledge => "/api/operations-agent/work-knowledge/",
        DemoSwitch.AgentHabitat => "/api/operations-agent/habitat/",
        _ => throw new ArgumentOutOfRangeException(nameof(demoSwitch), demoSwitch, "Unknown presenter switch.")
    };

    private static string Read(DemoSwitch demoSwitch, JsonElement payload) => demoSwitch switch
    {
        DemoSwitch.ToolSource => payload.GetProperty("source").GetString() ?? string.Empty,
        DemoSwitch.SecurityConsult => payload.GetProperty("enabled").GetBoolean() ? DemoSwitchValues.On : DemoSwitchValues.Off,
        DemoSwitch.WorkKnowledge => payload.GetProperty("evidencePresent").GetBoolean() ? DemoSwitchValues.EvidencePresent : DemoSwitchValues.EvidenceAbsent,
        DemoSwitch.AgentHabitat => payload.GetProperty("habitat").GetString() ?? string.Empty,
        _ => throw new ArgumentOutOfRangeException(nameof(demoSwitch), demoSwitch, "Unknown presenter switch.")
    };

    private static object Write(DemoSwitch demoSwitch, string value) => demoSwitch switch
    {
        DemoSwitch.ToolSource => new { source = value },
        DemoSwitch.SecurityConsult => new { enabled = string.Equals(value, DemoSwitchValues.On, StringComparison.OrdinalIgnoreCase) },
        DemoSwitch.WorkKnowledge => new { evidencePresent = string.Equals(value, DemoSwitchValues.EvidencePresent, StringComparison.OrdinalIgnoreCase) },
        DemoSwitch.AgentHabitat => new { habitat = value },
        _ => throw new ArgumentOutOfRangeException(nameof(demoSwitch), demoSwitch, "Unknown presenter switch.")
    };
}

internal static partial class SwitchClientLog
{
    [LoggerMessage(
        EventId = 2160,
        Level = LogLevel.Warning,
        Message = "Setting the {DemoSwitch} switch failed with status {StatusCode}. CorrelationId: {CorrelationId}.")]
    internal static partial void SetFailed(ILogger logger, DemoSwitch demoSwitch, int statusCode, string correlationId);
}
