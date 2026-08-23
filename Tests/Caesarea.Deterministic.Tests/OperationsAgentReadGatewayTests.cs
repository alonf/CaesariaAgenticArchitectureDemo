using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using OperationsAgent.Api.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class OperationsAgentReadGatewayTests
{
    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    [Fact]
    public async Task EnergyReadGatewayReadsStateAndPropagatesCorrelationId()
    {
        var state = CreateState();
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(state, options: SerializerOptions)
        });
        var gateway = new HttpEnergyReadGateway(CreateClient(handler), NullLogger<HttpEnergyReadGateway>.Instance);

        var result = await gateway.GetStateAsync(DemoAssets.StreetlightAssetId, "read-state-corr", CancellationToken.None);

        Assert.Equal(state, result);
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal("/api/energy/assets/L-417", handler.RequestUri?.PathAndQuery);
        Assert.Equal("read-state-corr", handler.CorrelationId);
    }

    [Fact]
    public async Task EnergyReadGatewaySurfacesDownstreamFailureWithoutFabricatingState()
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = JsonContent.Create(new ProblemDetails { Detail = "Energy Hub unavailable." })
        });
        var gateway = new HttpEnergyReadGateway(CreateClient(handler), NullLogger<HttpEnergyReadGateway>.Instance);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            gateway.GetStateAsync(DemoAssets.StreetlightAssetId, "read-failure-corr", CancellationToken.None));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
    }

    [Fact]
    public async Task CommandCenterReadGatewayReadsCustomerReportContext()
    {
        var context = new CustomerReportContext(DemoAssets.StreetlightAssetId, null);
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(context, options: SerializerOptions)
        });
        var gateway = new HttpCommandCenterReadGateway(CreateClient(handler), NullLogger<HttpCommandCenterReadGateway>.Instance);

        var result = await gateway.GetCustomerReportContextAsync(DemoAssets.StreetlightAssetId, "report-corr", CancellationToken.None);

        Assert.Null(result.Report);
        Assert.Equal("/api/command-center/customer-report/L-417", handler.RequestUri?.PathAndQuery);
        Assert.Equal("report-corr", handler.CorrelationId);
    }

    [Fact]
    public async Task CommandCenterReadGatewayReadsIncidentContext()
    {
        var incident = new IncidentContext(
            DemoAssets.StreetlightAssetId,
            new IncidentRecord(
                "INC-1",
                DemoAssets.StreetlightAssetId,
                DemoAssets.NorthPromenadeArea,
                "Streetlight on during daylight",
                "Anomaly already acknowledged.",
                IncidentSeverity.Warning,
                IncidentStatus.Open,
                DateTimeOffset.UtcNow,
                "incident-corr"));
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(incident, options: SerializerOptions)
        });
        var gateway = new HttpCommandCenterReadGateway(CreateClient(handler), NullLogger<HttpCommandCenterReadGateway>.Instance);

        var result = await gateway.GetIncidentContextAsync(DemoAssets.StreetlightAssetId, "incident-read-corr", CancellationToken.None);

        Assert.NotNull(result.Incident);
        Assert.Equal("INC-1", result.Incident!.Id);
        Assert.Equal("/api/command-center/incidents/current/L-417", handler.RequestUri?.PathAndQuery);
    }

    private static HttpClient CreateClient(HttpMessageHandler handler) =>
        new(handler)
        {
            BaseAddress = new Uri("http://localhost")
        };

    private static EnergyOperationalTwin CreateState() =>
        new(
            DemoAssets.StreetlightAssetId,
            DemoAssets.NorthPromenadeArea,
            true,
            true,
            true,
            false,
            true,
            ControllerHealthInfo.Healthy,
            null,
            DateTimeOffset.Parse("2026-08-23T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            true,
            null,
            DateTimeOffset.Parse("2026-08-23T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            OperationalContext.None);

    private sealed class RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string? CorrelationId { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            CorrelationId = request.Headers.TryGetValues(CorrelationHeaderNames.XCorrelationId, out var values)
                ? values.Single()
                : null;

            return Task.FromResult(responseFactory(request));
        }
    }
}
