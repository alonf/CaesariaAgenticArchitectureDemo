using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Security.Contracts;
using SecurityHub.Api.Services;

namespace Caesarea.Deterministic.Tests;

/// <summary>
/// The Security Hub's access rule, exercised over real HTTP. The MultiAgent stage claims a
/// permission boundary; a boundary nobody enforces is a convention, so the hub applies the rule
/// itself rather than trusting callers to stay away.
/// </summary>
public sealed class SecurityHubAccessTests
{
    [Fact]
    public async Task ReadingOperationsRequiresTheSecurityAgentCaller()
    {
        await using var factory = new WebApplicationFactory<SecurityHubService>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = factory.CreateClient();

        // No caller at all.
        using var anonymous = await client.GetAsync(
            $"/api/security/areas/{Uri.EscapeDataString(DemoAssets.NorthPromenadeArea)}/operations",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, anonymous.StatusCode);

        // The scenario service may seed operations, and may not read them.
        using var wrongCaller = CreateRequest(
            HttpMethod.Get,
            $"/api/security/areas/{Uri.EscapeDataString(DemoAssets.NorthPromenadeArea)}/operations",
            CallerIdentity.DemoScenario);
        using var wrongCallerResponse = await client.SendAsync(wrongCaller, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, wrongCallerResponse.StatusCode);

        using var permitted = CreateRequest(
            HttpMethod.Get,
            $"/api/security/areas/{Uri.EscapeDataString(DemoAssets.NorthPromenadeArea)}/operations",
            CallerIdentity.SecurityAgent);
        using var permittedResponse = await client.SendAsync(permitted, TestContext.Current.CancellationToken);
        permittedResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task SeedingOperationsRequiresTheScenarioCaller()
    {
        await using var factory = new WebApplicationFactory<SecurityHubService>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = factory.CreateClient();
        var payload = new SecurityScenarioSyncRequest([], "Test sync.");

        // The Security Agent may read operations, and may not write them.
        using var wrongCaller = CreateRequest(HttpMethod.Post, "/api/security/admin/scenario", CallerIdentity.SecurityAgent);
        wrongCaller.Content = JsonContent.Create(payload, options: CaesareaJsonDefaults.CreateSerializerOptions());
        using var wrongCallerResponse = await client.SendAsync(wrongCaller, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, wrongCallerResponse.StatusCode);

        using var permitted = CreateRequest(HttpMethod.Post, "/api/security/admin/scenario", CallerIdentity.DemoScenario);
        permitted.Content = JsonContent.Create(payload, options: CaesareaJsonDefaults.CreateSerializerOptions());
        using var permittedResponse = await client.SendAsync(permitted, TestContext.Current.CancellationToken);
        permittedResponse.EnsureSuccessStatusCode();
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string relativeUri, string caller)
    {
        var request = new HttpRequestMessage(method, relativeUri);
        request.Headers.Add(CallerIdentity.HeaderName, caller);
        return request;
    }
}
