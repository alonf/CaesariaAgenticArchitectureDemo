using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using CommandCenter.Web.Configuration;
using Microsoft.Extensions.Options;

namespace CommandCenter.Web.Services;

/// <summary>
/// Talks to the Foundry-hosted Operations Agent over its Responses protocol endpoint - the same
/// call deploy-hosted-agent.yml's smoke test makes, from inside the local demo.
/// <para>
/// The token is minted for the Foundry data plane with the presenter's own credential (az login,
/// Visual Studio, whatever DefaultAzureCredential finds), and that is the demo's point rather than
/// a convenience: the platform reads the caller out of that token and hands it to the agent as
/// x-agent-user-id, so Work IQ answers with what the presenter can see. No identity appears in this
/// repository or its configuration.
/// </para>
/// </summary>
internal sealed class HostedOperationsAgentClient
{
    private static readonly TokenRequestContext TokenContext = new(["https://ai.azure.com/.default"]);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly TokenCredential _credential;
    private readonly CommandCenterWebOptions.HostedAgentOptions _options;
    private readonly ILogger<HostedOperationsAgentClient> _logger;

    public HostedOperationsAgentClient(
        HttpClient httpClient,
        TokenCredential credential,
        IOptions<CommandCenterWebOptions> options,
        ILogger<HostedOperationsAgentClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        _options = options?.Value.HostedAgent ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HostedAgentOutcome> AskAsync(string question, CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            return new HostedAgentOutcome(
                false,
                null,
                "The hosted agent is not configured. Set CommandCenterWeb:HostedAgent:ProjectEndpoint.");
        }

        AccessToken token;

        try
        {
            token = await _credential.GetTokenAsync(TokenContext, cancellationToken);
        }
        catch (Exception exception) when (exception is Azure.Identity.CredentialUnavailableException
            or Azure.Identity.AuthenticationFailedException)
        {
            // The most likely first failure on a new machine, so it names its own fix.
            return new HostedAgentOutcome(
                false,
                null,
                $"No Azure sign-in for the hosted call. Run 'az login' as the presenter and retry. ({exception.Message})");
        }

        var url = $"{_options.ProjectEndpoint.TrimEnd('/')}/agents/{Uri.EscapeDataString(_options.AgentName)}"
            + "/endpoint/protocols/openai/responses?api-version=v1";

        // One identifier spans the whole operation: our own correlation header (for the Command
        // Center's logs) and the client-request id Azure services echo into their diagnostics. A
        // hosted turn can run for a minute; without these it is unfindable across App Insights.
        var correlationId = CorrelationIds.Create();
        HostedAgentLog.Asking(_logger, _options.AgentName, correlationId);

        // store: false, like the release smoke test: one question, one answer, nothing persisted.
        // The hosted panel is a single-shot beat, not a second conversational session.
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new HostedAskRequest(question), options: SerializerOptions)
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, correlationId);
        request.Headers.Add("x-ms-client-request-id", correlationId);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            HostedAgentLog.Failed(_logger, _options.AgentName, correlationId, (int)response.StatusCode);
            return new HostedAgentOutcome(
                false,
                null,
                $"The hosted agent endpoint returned HTTP {(int)response.StatusCode}. {ExtractErrorMessage(body)}".TrimEnd());
        }

        HostedAskResponse? parsed;

        try
        {
            parsed = JsonSerializer.Deserialize<HostedAskResponse>(body, SerializerOptions);
        }
        catch (JsonException)
        {
            parsed = null;
        }

        if (parsed is null)
        {
            return new HostedAgentOutcome(false, null, "The hosted agent returned a response this client could not read.");
        }

        // Work IQ asks the CALLING USER for OAuth consent the first time it acts for them: the run
        // comes back 'incomplete' with an oauth_consent_request item instead of text. That is not a
        // failure - it is delegated access made visible - so it surfaces as a link for the presenter
        // to open, not as an error. Consent granted, the same question succeeds.
        var consent = (parsed.Output ?? [])
            .FirstOrDefault(item =>
                string.Equals(item.Type, "oauth_consent_request", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(item.ConsentLink));

        if (consent is not null)
        {
            HostedAgentLog.ConsentRequired(_logger, _options.AgentName, correlationId, parsed.Id ?? "(none)");
            return new HostedAgentOutcome(false, null, null, consent.ConsentLink, parsed.Id);
        }

        if (!string.Equals(parsed.Status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            var detail = parsed.Error?.Message ?? "no further detail was reported";
            HostedAgentLog.Incomplete(_logger, _options.AgentName, correlationId, parsed.Status ?? "unknown", parsed.Id ?? "(none)");
            return new HostedAgentOutcome(
                false,
                null,
                $"The hosted run ended with status '{parsed.Status ?? "unknown"}': {detail}.",
                ResponseId: parsed.Id);
        }

        // Reasoning items carry no text; the answer is whatever output content does. Same
        // extraction as the release smoke test's jq: .output[]?.content[]?.text // empty.
        var answer = string.Join(
            "\n\n",
            (parsed.Output ?? [])
                .SelectMany(item => item.Content ?? [])
                .Select(content => content.Text)
                .Where(text => !string.IsNullOrWhiteSpace(text)));

        if (string.IsNullOrWhiteSpace(answer))
        {
            HostedAgentLog.Incomplete(_logger, _options.AgentName, correlationId, "completed-empty", parsed.Id ?? "(none)");
            return new HostedAgentOutcome(false, null, "The hosted run completed but produced no text output.", ResponseId: parsed.Id);
        }

        HostedAgentLog.Answered(_logger, _options.AgentName, correlationId, parsed.Id ?? "(none)");
        return new HostedAgentOutcome(true, answer, null, ResponseId: parsed.Id);
    }

    private static string ExtractErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        try
        {
            var error = JsonSerializer.Deserialize<HostedErrorEnvelope>(body, SerializerOptions);
            return error?.Error?.Message ?? string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private sealed record HostedAskRequest(
        [property: JsonPropertyName("input")] string Input,
        [property: JsonPropertyName("store")] bool Store = false);

    private sealed record HostedAskResponse(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("output")] IReadOnlyList<HostedOutputItem>? Output,
        [property: JsonPropertyName("error")] HostedError? Error);

    private sealed record HostedOutputItem(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("content")] IReadOnlyList<HostedOutputContent>? Content,
        [property: JsonPropertyName("consent_link")] string? ConsentLink);

    private sealed record HostedOutputContent(
        [property: JsonPropertyName("text")] string? Text);

    private sealed record HostedError(
        [property: JsonPropertyName("message")] string? Message);

    private sealed record HostedErrorEnvelope(
        [property: JsonPropertyName("error")] HostedError? Error);
}

/// <summary>
/// What became of one question to the hosted agent: an answer in the agent's own markdown, a
/// reason it could not be obtained, or - first use per person - the Work IQ consent link the
/// presenter must open before the platform will act on their behalf. The Foundry response id rides
/// along so a long hosted run can be found again in the platform's own diagnostics.
/// </summary>
internal sealed record HostedAgentOutcome(
    bool Succeeded,
    string? Answer,
    string? FailureMessage,
    string? ConsentLink = null,
    string? ResponseId = null);

internal static partial class HostedAgentLog
{
    [LoggerMessage(EventId = 2801, Level = LogLevel.Information,
        Message = "Asking the hosted agent {AgentName}. CorrelationId: {CorrelationId}.")]
    internal static partial void Asking(ILogger logger, string agentName, string correlationId);

    [LoggerMessage(EventId = 2802, Level = LogLevel.Information,
        Message = "Hosted agent {AgentName} answered. CorrelationId: {CorrelationId}. ResponseId: {ResponseId}.")]
    internal static partial void Answered(ILogger logger, string agentName, string correlationId, string responseId);

    [LoggerMessage(EventId = 2803, Level = LogLevel.Information,
        Message = "Hosted agent {AgentName} requires Work IQ consent. CorrelationId: {CorrelationId}. ResponseId: {ResponseId}.")]
    internal static partial void ConsentRequired(ILogger logger, string agentName, string correlationId, string responseId);

    [LoggerMessage(EventId = 2804, Level = LogLevel.Warning,
        Message = "Hosted agent {AgentName} run ended '{Status}'. CorrelationId: {CorrelationId}. ResponseId: {ResponseId}.")]
    internal static partial void Incomplete(ILogger logger, string agentName, string correlationId, string status, string responseId);

    [LoggerMessage(EventId = 2805, Level = LogLevel.Warning,
        Message = "Hosted agent {AgentName} call with CorrelationId {CorrelationId} returned HTTP {StatusCode}.")]
    internal static partial void Failed(ILogger logger, string agentName, string correlationId, int statusCode);
}
