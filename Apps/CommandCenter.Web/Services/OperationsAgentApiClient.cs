using System.Text.Json;
using CommandCenter.Web.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CommandCenter.Web.Services;

internal sealed class OperationsAgentApiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();
    private readonly HttpClient _httpClient;
    private readonly CommandCenterWebOptions _options;

    public OperationsAgentApiClient(HttpClient httpClient, IOptions<CommandCenterWebOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.AssetId);
    }

    public async Task<InvestigationOutcome> InvestigateAsync(CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIds.Create();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/operations-agent/assets/{Uri.EscapeDataString(_options.AssetId)}/investigate");
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, correlationId);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<InvestigationResult>(SerializerOptions, cancellationToken)
                ?? throw new InvalidOperationException("Operations Agent investigation response was empty.");
            return new InvestigationOutcome(true, result, null);
        }

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(SerializerOptions, cancellationToken);
        return new InvestigationOutcome(false, null, problem?.Detail ?? "The Operations Agent investigation could not be completed.");
    }
}

internal sealed record InvestigationOutcome(bool Succeeded, InvestigationResult? Result, string? FailureMessage);
