using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Workforce.Contracts;
using WorkforceHub.Api.Services;

namespace Caesarea.Deterministic.Tests;

/// <summary>
/// The workforce boundary, which is enforced one level lower than the Security one.
/// <para>
/// The Security Agent reads restricted records and its <em>output</em> is sanitized. This domain
/// never hands its agent the sensitive fields at all: the extraction returns a projection, so the
/// agent has nothing to disclose however it is asked. These tests pin that the projection really is
/// a projection - not a redaction of a fuller payload that briefly existed on the wire.
/// </para>
/// </summary>
public sealed class WorkforceBoundaryTests
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    [Fact]
    public void TheShareableProjectionCarriesNoCommercialOrPersonalField()
    {
        // Written against the type, so adding a sensitive field to the projection fails here rather
        // than quietly widening what every caller receives.
        var shareableFields = typeof(ShareableWorkOrderDetails)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain("TechnicianName", shareableFields);
        Assert.DoesNotContain("TechnicianBadge", shareableFields);
        Assert.DoesNotContain("LabourCost", shareableFields);
        Assert.DoesNotContain("CallOutRate", shareableFields);
        Assert.DoesNotContain("TechnicianNote", shareableFields);

        // And it still carries what another domain legitimately needs.
        Assert.Contains("Reason", shareableFields);
        Assert.Contains("Status", shareableFields);
        Assert.Contains("ExpectedClearanceAt", shareableFields);
        Assert.Contains("OperationalNote", shareableFields);
    }

    [Fact]
    public async Task TheSensitiveFieldsNeverAppearOnTheWireToTheAgent()
    {
        // The projection is what leaves the hub, so the serialized response is checked as text: a
        // field that is absent from the type cannot come back as a stray JSON property either.
        await using var factory = CreateHub();
        using var client = factory.CreateClient();

        using var request = CreateRequest(HttpMethod.Get, "/api/workforce/work-orders/WO-8732/shareable", CallerIdentity.WorkforceAgent);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        foreach (var secret in (string[])["Cohen", "4471", "1240", "Premium call-out", "premium", "bills at"])
        {
            Assert.DoesNotContain(secret, payload, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("post-maintenance verification", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TheSearchResultIsAlsoADisclosureSurfaceAndCarriesNothingSensitive()
    {
        await using var factory = CreateHub();
        using var client = factory.CreateClient();

        using var request = CreateRequest(
            HttpMethod.Get, $"/api/workforce/assets/{DemoAssets.StreetlightAssetId}/work-orders", CallerIdentity.WorkforceAgent);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        foreach (var secret in (string[])["Cohen", "Mizrahi", "4471", "3902", "1240", "310.0", "call-out"])
        {
            Assert.DoesNotContain(secret, payload, StringComparison.OrdinalIgnoreCase);
        }

        // Two work orders, so choosing the relevant one is a real step for the agent.
        var summaries = await response.Content.ReadFromJsonAsync<IReadOnlyList<WorkOrderSummary>>(
            JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(2, summaries!.Count);
    }

    [Fact]
    public async Task OnlyTheWorkforceAgentMayReadWorkOrders()
    {
        await using var factory = CreateHub();
        using var client = factory.CreateClient();

        using var anonymous = await client.GetAsync(
            "/api/workforce/work-orders/WO-8732/shareable", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, anonymous.StatusCode);

        // The scenario service may seed work orders and may not read them.
        using var wrongCaller = CreateRequest(
            HttpMethod.Get, "/api/workforce/work-orders/WO-8732/shareable", CallerIdentity.DemoScenario);
        using var wrongCallerResponse = await client.SendAsync(wrongCaller, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, wrongCallerResponse.StatusCode);
    }

    [Fact]
    public async Task AConnectionWithNoAddressAtAllIsNotThePresenter()
    {
        // The default shape of a request that never touched a socket. It is its own case because
        // the rule reads the address, and "there is no address" must fail closed rather than being
        // treated as a local one.
        await using var factory = CreateHub();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/workforce-records");
        request.Headers.Add(CallerIdentity.HeaderName, CallerIdentity.DemoControl);
        using var client = factory.CreateClient();

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(null, true, HttpStatusCode.Forbidden)]
    [InlineData(CallerIdentity.WorkforceAgent, true, HttpStatusCode.Forbidden)]
    [InlineData(CallerIdentity.DemoScenario, true, HttpStatusCode.Forbidden)]
    [InlineData(CallerIdentity.DemoControl, false, HttpStatusCode.Forbidden)]
    [InlineData(CallerIdentity.DemoControl, true, HttpStatusCode.OK)]
    public async Task TheFullRecordNeedsBothThePresentersNameAndALoopbackConnection(
        string? caller, bool fromLoopback, HttpStatusCode expected)
    {
        // This is the route that serves technician names, badges, costs and rates, and the two
        // conditions on it are not the same condition. Loopback alone identifies nobody: every
        // service in this demo runs on the presenter's machine, so "local" would admit the
        // Operations Agent as readily as the switchboard. The header alone is a string anyone can
        // send. Only together do they mean "the presenter, at the keyboard".
        await using var factory = CreateHub(fromLoopback ? IPAddress.Loopback : IPAddress.Parse("203.0.113.9"));
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/workforce-records");

        if (caller is not null)
        {
            request.Headers.Add(CallerIdentity.HeaderName, caller);
        }

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(expected, response.StatusCode);

        if (expected is not HttpStatusCode.OK)
        {
            // A refusal must refuse the payload too, not merely the status line.
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.DoesNotContain("4471", body, StringComparison.Ordinal);
            Assert.DoesNotContain("Cohen", body, StringComparison.Ordinal);
        }
    }

    private static WebApplicationFactory<WorkforceHubService> CreateHub() =>
        new WebApplicationFactory<WorkforceHubService>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development"));

    // The test host opens no socket, so a request arrives with no connection address at all and
    // the loopback branch is unreachable by default - which is why the plain CreateHub() overload
    // can only ever observe the "nobody" case. Stamping the address the transport would have set
    // lets both halves of the rule be exercised against the real route.
    private static WebApplicationFactory<WorkforceHubService> CreateHub(IPAddress remoteAddress) =>
        new WebApplicationFactory<WorkforceHubService>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureTestServices(services =>
                    services.AddSingleton<IStartupFilter>(new StampConnectionAddress(remoteAddress)));
            });

    private static HttpRequestMessage CreateRequest(HttpMethod method, string relativeUri, string caller)
    {
        var request = new HttpRequestMessage(method, relativeUri);
        request.Headers.Add(CallerIdentity.HeaderName, caller);
        return request;
    }

    private sealed class StampConnectionAddress(IPAddress address) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, following) =>
            {
                context.Connection.RemoteIpAddress = address;
                await following(context);
            });

            next(app);
        };
    }
}
