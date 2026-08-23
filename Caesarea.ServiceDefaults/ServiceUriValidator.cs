using System.Diagnostics.CodeAnalysis;

namespace Caesarea.ServiceDefaults;

/// <summary>
/// Validates HTTP service endpoint URIs, including Aspire service-discovery addresses such as <c>https+http://service-name</c>.
/// </summary>
public static class ServiceUriValidator
{
    private static readonly HashSet<string> SupportedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        Uri.UriSchemeHttp,
        Uri.UriSchemeHttps,
        "http+https",
        "https+http"
    };

    /// <summary>
    /// Validates that the supplied string is an absolute URI with a non-empty host component.
    /// </summary>
    /// <param name="value">The URI string to validate.</param>
    /// <param name="uri">The parsed URI when validation succeeds; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the value is a valid absolute or service-discovery URI; otherwise <see langword="false"/>.</returns>
    public static bool TryValidateAbsoluteOrServiceDiscoveryUri(string? value, [NotNullWhen(true)] out Uri? uri)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsedUri)
            || string.IsNullOrWhiteSpace(parsedUri.Host)
            || !SupportedSchemes.Contains(parsedUri.Scheme))
        {
            uri = null;
            return false;
        }

        uri = parsedUri;
        return true;
    }
}
