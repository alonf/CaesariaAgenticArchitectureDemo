using OperationsAgent.Api.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class McpEndpointTests
{
    [Theory]
    [InlineData("http://localhost:5016", "http://localhost:5016/mcp")]
    [InlineData("http://localhost:5016/", "http://localhost:5016/mcp")]
    [InlineData("https://energyhub.example", "https://energyhub.example/mcp")]
    public void KeepsConcreteSchemes(string baseUri, string expected)
    {
        Assert.Equal(new Uri(expected), McpEndpoint.Create(baseUri));
    }

    [Theory]
    [InlineData("https+http://energyhub-api", "https://energyhub-api/mcp")]
    [InlineData("http+https://energyhub-api/", "http://energyhub-api/mcp")]
    public void ReducesAspireCompoundSchemesToThePreferredScheme(string baseUri, string expected)
    {
        // The MCP transport rejects compound service-discovery schemes; the preferred (first)
        // scheme is kept and the service-discovery handler still resolves the logical host.
        Assert.Equal(new Uri(expected), McpEndpoint.Create(baseUri));
    }
}
