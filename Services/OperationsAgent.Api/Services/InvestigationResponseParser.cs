using System.Text.Json;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Parses and strictly validates the raw JSON produced by the investigation model against the required
/// evidence-discipline schema. This type performs no model or network calls, which keeps it fully testable
/// without a live Microsoft Foundry connection.
/// </summary>
public static class InvestigationResponseParser
{
    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();
    private static readonly HashSet<string> AllowedFactSources =
    [
        OperationsToolset.CustomerReportToolName,
        OperationsToolset.EnergyAssetStateToolName,
        OperationsToolset.EnergyRecentActivityToolName,
        OperationsToolset.IncidentContextToolName
    ];

    /// <summary>
    /// Parses and validates the supplied raw model response text.
    /// </summary>
    /// <param name="rawJson">The raw JSON text returned by the investigation model.</param>
    /// <returns>The validated model investigation response.</returns>
    /// <exception cref="InvestigationResponseFormatException">
    /// Thrown when the response is empty, is not valid JSON, or does not satisfy the required schema.
    /// </exception>
    public static ModelInvestigationResponse Parse(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            throw new InvestigationResponseFormatException("The investigation model returned an empty response.");
        }

        ModelInvestigationResponse? response;

        try
        {
            response = JsonSerializer.Deserialize<ModelInvestigationResponse>(rawJson, SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new InvestigationResponseFormatException("The investigation model response was not valid JSON.", exception);
        }

        if (response is null)
        {
            throw new InvestigationResponseFormatException("The investigation model returned a null response.");
        }

        Validate(response);
        return response;
    }

    private static void Validate(ModelInvestigationResponse response)
    {
        if (response.VerifiedFacts is null || response.Hypotheses is null || response.MissingEvidence is null)
        {
            throw new InvestigationResponseFormatException("The investigation model response is missing a required collection.");
        }

        if (string.IsNullOrWhiteSpace(response.Summary))
        {
            throw new InvestigationResponseFormatException("The investigation model response is missing a summary.");
        }

        foreach (var fact in response.VerifiedFacts)
        {
            if (fact is null || string.IsNullOrWhiteSpace(fact.Statement) || string.IsNullOrWhiteSpace(fact.Source))
            {
                throw new InvestigationResponseFormatException("A verified fact is missing its statement or source.");
            }

            if (!AllowedFactSources.Contains(fact.Source))
            {
                throw new InvestigationResponseFormatException(
                    $"Verified fact source '{fact.Source}' is not a stable read-only tool name.");
            }
        }

        foreach (var hypothesis in response.Hypotheses)
        {
            if (hypothesis is null || string.IsNullOrWhiteSpace(hypothesis.Statement) || string.IsNullOrWhiteSpace(hypothesis.Reason))
            {
                throw new InvestigationResponseFormatException("A hypothesis is missing its statement or reason.");
            }

            if (hypothesis.Confidence is < 0 or > 1 || double.IsNaN(hypothesis.Confidence))
            {
                throw new InvestigationResponseFormatException(
                    $"Hypothesis confidence {hypothesis.Confidence} is outside the required 0-1 range.");
            }
        }

        foreach (var missing in response.MissingEvidence)
        {
            if (string.IsNullOrWhiteSpace(missing))
            {
                throw new InvestigationResponseFormatException("A missing-evidence entry cannot be blank.");
            }
        }
    }
}
