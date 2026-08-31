namespace OperationsAgent.Api.Services;

/// <summary>
/// Builds the Energy Hub MCP endpoint from the configured base URI. The MCP transport requires a
/// concrete http or https scheme, while Aspire service-discovery URIs may carry a compound scheme
/// (for example <c>https+http://energyhub-api</c>, meaning prefer https, fall back to http). The
/// compound scheme is reduced to its first (preferred) scheme; the service-discovery handler on
/// the transport's HttpClient still resolves the logical host name at request time.
/// </summary>
internal static class McpEndpoint
{
    /// <summary>
    /// Creates the absolute MCP endpoint for the supplied Energy Hub base URI.
    /// </summary>
    /// <param name="energyHubBaseUri">The configured Energy Hub base URI.</param>
    /// <returns>The MCP endpoint with a concrete http or https scheme.</returns>
    public static Uri Create(string energyHubBaseUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(energyHubBaseUri);

        var baseUri = energyHubBaseUri.TrimEnd('/');
        var schemeSeparator = baseUri.IndexOf("://", StringComparison.Ordinal);

        if (schemeSeparator > 0)
        {
            var scheme = baseUri[..schemeSeparator];
            var preferredSchemeLength = scheme.IndexOf('+');

            if (preferredSchemeLength > 0)
            {
                baseUri = string.Concat(scheme.AsSpan(0, preferredSchemeLength), baseUri.AsSpan(schemeSeparator));
            }
        }

        return new Uri($"{baseUri}/mcp", UriKind.Absolute);
    }
}
