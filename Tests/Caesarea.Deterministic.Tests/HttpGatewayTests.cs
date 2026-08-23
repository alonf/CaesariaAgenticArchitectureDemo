using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CommandCenter.Api.Services;
using DemoScenario.Api.Services;
using EnergyHub.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Caesarea.Deterministic.Tests;

public sealed class HttpGatewayTests
{
    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    [Fact]
    public async Task SmartPoleGatewayReadsStateAndPropagatesCorrelationId()
    {
        var state = CreatePhysicalState();
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(state, options: SerializerOptions)
        });
        var gateway = new HttpSmartPoleGateway(CreateClient(handler), TimeProvider.System, NullLogger<HttpSmartPoleGateway>.Instance);

        var result = await gateway.GetStateAsync(DemoAssets.StreetlightAssetId, "smartpole-state-corr", CancellationToken.None);

        Assert.Equal(state, result);
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal("/api/smartpole/state/L-417", handler.RequestUri?.PathAndQuery);
        Assert.Equal("smartpole-state-corr", handler.CorrelationId);
    }

    [Fact]
    public async Task SmartPoleGatewayMapsGatewayTimeoutWithoutInventingPhysicalState()
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.GatewayTimeout)
        {
            Content = JsonContent.Create(new ProblemDetails { Detail = "Device acknowledgement timed out." })
        });
        var gateway = new HttpSmartPoleGateway(CreateClient(handler), TimeProvider.System, NullLogger<HttpSmartPoleGateway>.Instance);

        var result = await gateway.SetLampStateAsync(
            new SetLampStateCommand(DemoAssets.StreetlightAssetId, false),
            "smartpole-timeout-corr",
            CancellationToken.None);

        Assert.Equal(CommandExecutionStatus.TimedOut, result.Status);
        Assert.Null(result.ActualIsOn);
        Assert.Equal("Device acknowledgement timed out.", result.Summary);
    }

    [Fact]
    public async Task EnergyGatewayPreservesUnknownStateWhenFailurePayloadOmitsIt()
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = JsonContent.Create(new ProblemDetails { Detail = "Energy Hub unavailable." })
        });
        var gateway = new HttpEnergyHubGateway(CreateClient(handler), TimeProvider.System, NullLogger<HttpEnergyHubGateway>.Instance);

        var result = await gateway.RestoreScheduledModeAsync(
            DemoAssets.StreetlightAssetId,
            "energy-failure-corr",
            CancellationToken.None);

        Assert.Equal(CommandExecutionStatus.Failed, result.Status);
        Assert.Null(result.DesiredIsOn);
        Assert.Null(result.ReportedIsOn);
        Assert.Equal("energy-failure-corr", handler.CorrelationId);
    }

    [Fact]
    public async Task ScenarioClientPostsPayloadAndCorrelationId()
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = new HttpSmartPoleScenarioClient(CreateClient(handler), NullLogger<HttpSmartPoleScenarioClient>.Instance);
        var scenario = new SmartPoleScenarioState(
            true,
            true,
            false,
            true,
            ControllerHealthInfo.Healthy,
            null,
            false,
            OperationalContext.None,
            SmartPoleBehaviorConfiguration.Default);

        await client.ApplyScenarioAsync(scenario, "scenario-corr", CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("/api/smartpole/scenario", handler.RequestUri?.PathAndQuery);
        Assert.Equal("scenario-corr", handler.CorrelationId);
        Assert.Contains("\"isOn\":true", handler.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ScenarioClientSurfacesDownstreamFailure()
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = JsonContent.Create(new ProblemDetails { Detail = "Scenario rejected." })
        });
        var client = new HttpEnergyScenarioClient(CreateClient(handler), NullLogger<HttpEnergyScenarioClient>.Instance);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.ApplyScenarioAsync(
                new EnergyScenarioSyncRequest(false, null, "Synchronize normal operation."),
                "scenario-failure-corr",
                CancellationToken.None));

        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
    }

    private static HttpClient CreateClient(HttpMessageHandler handler) =>
        new(handler)
        {
            BaseAddress = new Uri("http://localhost")
        };

    private static SmartPolePhysicalState CreatePhysicalState() =>
        new(
            DemoAssets.StreetlightAssetId,
            DemoAssets.NorthPromenadeArea,
            true,
            true,
            false,
            true,
            ControllerHealthInfo.Healthy,
            null,
            null,
            false,
            OperationalContext.None,
            DateTimeOffset.Parse("2026-08-23T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            SmartPoleBehaviorConfiguration.Default);

    private sealed class RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string? CorrelationId { get; private set; }

        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            CorrelationId = request.Headers.TryGetValues(CorrelationHeaderNames.XCorrelationId, out var values)
                ? values.Single()
                : null;
            Body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return responseFactory(request);
        }
    }
}
