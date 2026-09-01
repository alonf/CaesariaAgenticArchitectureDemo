using System.Net.Http.Json;
using EnergyHub.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OperationsAgent.Api.Services;

namespace Caesarea.Deterministic.Tests;

/// <summary>
/// Drives the real Energy Hub endpoint with the real <see cref="HttpEnergyCommandGateway"/>, so
/// the state-precondition contract is proven end to end over HTTP rather than asserted against a
/// fake. A refused precondition must be distinguishable from a downstream failure on the wire -
/// the workflow's whole re-validation path depends on telling them apart.
/// </summary>
public sealed class EnergyCommandGatewayIntegrationTests
{
    [Fact]
    public async Task StaleRevisionIsRefusedAsAPreconditionFailureOverHttp()
    {
        await using var factory = CreateEnergyHub();
        using var httpClient = factory.CreateClient();
        var gateway = CreateGateway(httpClient);

        var before = await ReadTwinAsync(httpClient);

        // A revision the Hub has moved past: the command must be refused, and the refusal must be
        // recognizable as a precondition failure, not reported as a plain command failure.
        var outcome = await gateway.RestoreScheduledModeAsync(
            DemoAssets.StreetlightAssetId, "stale-precondition-corr", before.StateRevision - 1, TestContext.Current.CancellationToken);

        Assert.True(outcome.PreconditionFailed);
        Assert.Contains("revision", outcome.Summary, StringComparison.OrdinalIgnoreCase);

        // And nothing happened: the twin is untouched, revision included.
        Assert.Equal(before, await ReadTwinAsync(httpClient));
    }

    [Fact]
    public async Task CurrentRevisionIsAcceptedAndExecutes()
    {
        await using var factory = CreateEnergyHub();
        using var httpClient = factory.CreateClient();
        var gateway = CreateGateway(httpClient);

        var revision = await ReadStateRevisionAsync(httpClient);

        var outcome = await gateway.RestoreScheduledModeAsync(
            DemoAssets.StreetlightAssetId, "fresh-precondition-corr", revision, TestContext.Current.CancellationToken);

        Assert.False(outcome.PreconditionFailed);
        Assert.Equal(CommandExecutionStatus.Succeeded, outcome.Status);

        var twin = await ReadTwinAsync(httpClient);
        Assert.False(twin.ReportedIsOn);
        Assert.False(twin.ManualOverride);
        // Accepting the command moved the revision on, so the same one cannot be replayed.
        Assert.NotEqual(revision, twin.StateRevision);

        var replay = await gateway.RestoreScheduledModeAsync(
            DemoAssets.StreetlightAssetId, "replay-corr", revision, TestContext.Current.CancellationToken);
        Assert.True(replay.PreconditionFailed);
    }

    [Fact]
    public async Task DownstreamFailureIsNotReportedAsAPreconditionFailure()
    {
        var gateway = new FailingSmartPoleGateway();
        await using var factory = CreateEnergyHub(gateway);
        using var httpClient = factory.CreateClient();
        var commandGateway = CreateGateway(httpClient);

        var outcome = await commandGateway.RestoreScheduledModeAsync(
            DemoAssets.StreetlightAssetId, "downstream-failure-corr", await ReadStateRevisionAsync(httpClient), TestContext.Current.CancellationToken);

        // A boundary that failed is a failure the workflow reports and raises a work item for -
        // it must never be mistaken for "your picture is stale, try again".
        Assert.False(outcome.PreconditionFailed);
        Assert.NotEqual(CommandExecutionStatus.Succeeded, outcome.Status);
    }

    private static WebApplicationFactory<EnergyMcpTools> CreateEnergyHub(ISmartPoleGateway? smartPoleGateway = null)
    {
        var gateway = smartPoleGateway ?? new FakeSmartPoleGateway { PhysicalState = CreateOverriddenDaylightState() };
        return new WebApplicationFactory<EnergyMcpTools>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISmartPoleGateway>();
                services.AddSingleton(gateway);
            });
        });
    }

    private static HttpEnergyCommandGateway CreateGateway(HttpClient httpClient) =>
        new(httpClient, NullLogger<HttpEnergyCommandGateway>.Instance);

    private static async Task<EnergyOperationalTwin> ReadTwinAsync(HttpClient httpClient) =>
        await httpClient.GetFromJsonAsync<EnergyOperationalTwin>(
            $"/api/energy/assets/{DemoAssets.StreetlightAssetId}",
            CaesareaJsonDefaults.CreateSerializerOptions(),
            TestContext.Current.CancellationToken)
        ?? throw new InvalidOperationException("The Energy Hub returned no twin.");

    private static async Task<long> ReadStateRevisionAsync(HttpClient httpClient) =>
        (await ReadTwinAsync(httpClient)).StateRevision;

    private static SmartPolePhysicalState CreateOverriddenDaylightState() => new(
        DemoAssets.StreetlightAssetId,
        DemoAssets.NorthPromenadeArea,
        IsOn: true,
        IsDaylight: true,
        ExpectedScheduledState: false,
        ManualOverride: true,
        ControllerHealthInfo.Healthy,
        LastCommand: null,
        LastMaintenanceTime: null,
        HasRecentMaintenance: false,
        OperationalContext.None,
        LastReportedAt: new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero),
        SmartPoleBehaviorConfiguration.Default);

    private sealed class FailingSmartPoleGateway : ISmartPoleGateway
    {
        public Task<SmartPolePhysicalState> GetStateAsync(string assetId, string correlationId, CancellationToken cancellationToken) =>
            Task.FromResult(CreateOverriddenDaylightState());

        public Task<SmartPoleCommandResult> SetLampStateAsync(SetLampStateCommand command, string correlationId, CancellationToken cancellationToken) =>
            throw new HttpRequestException("SmartPole is unreachable.");
    }
}
