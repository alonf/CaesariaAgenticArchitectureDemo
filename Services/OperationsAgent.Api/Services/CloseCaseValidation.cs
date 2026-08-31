using System.Text.RegularExpressions;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Validates close-case requests at the API boundary. Stored case text later reaches the model as
/// recalled context, so the boundary is strict: a canonical asset identifier and hard length caps
/// on the free-text fields.
/// </summary>
internal static partial class CloseCaseValidation
{
    internal const int MaxSymptomLength = 200;
    internal const int MaxResolutionLength = 1000;

    /// <summary>
    /// Validates the supplied request.
    /// </summary>
    /// <param name="request">The close-case request to validate.</param>
    /// <returns>A human-readable validation error, or <see langword="null"/> when the request is valid.</returns>
    public static string? Validate(OperationsAgentCloseCaseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.AssetId) || !AssetIdRegex().IsMatch(request.AssetId))
        {
            return "The asset identifier must match the canonical form, for example L-417.";
        }

        if (string.IsNullOrWhiteSpace(request.Symptom) || request.Symptom.Length > MaxSymptomLength)
        {
            return $"The symptom is required and must be at most {MaxSymptomLength} characters.";
        }

        if (string.IsNullOrWhiteSpace(request.Resolution) || request.Resolution.Length > MaxResolutionLength)
        {
            return $"The resolution is required and must be at most {MaxResolutionLength} characters.";
        }

        return null;
    }

    [GeneratedRegex(@"^[A-Za-z]{1,4}-\d{1,6}$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AssetIdRegex();
}
