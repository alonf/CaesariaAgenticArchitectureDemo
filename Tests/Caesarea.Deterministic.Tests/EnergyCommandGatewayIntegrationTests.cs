using DurableOperations.Contracts;
using EnergyHub.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OperationsAgent.Api.Services;
using OperationsAgent.Functions;

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
    public async Task InterruptedActivityReconcilesPendingHubCommandBeforeItCompletes()
    {
        var accepted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishDeviceCommand = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var deviceCommands = 0;
        var pole = new FakeSmartPoleGateway
        {
            PhysicalState = CreateOverriddenDaylightState(),
            OnSetLampStateAsync = async (command, correlation, token) =>
            {
                Interlocked.Increment(ref deviceCommands);
                accepted.SetResult();
                await finishDeviceCommand.Task.WaitAsync(token);
                return new SmartPoleCommandResult(command.AssetId, command.DesiredIsOn!.Value,
                    CommandExecutionStatus.Succeeded, correlation, "Device finished after interruption.", DateTimeOffset.UtcNow);
            }
        };
        await using var factory = CreateEnergyHub(pole);
        using var client = factory.CreateClient();
        using var seeded = await client.PostAsJsonAsync("/api/energy/admin/scenario",
            new EnergyScenarioSyncRequest(false, null, "Pending command recovery test"), TestContext.Current.CancellationToken);
        seeded.EnsureSuccessStatusCode();
        var twin = await ReadTwinAsync(client);
        var clock = new TestTimeProvider();
        var now = clock.GetUtcNow();
        var authorization = new RecoveryAuthorization("operator", "supervisor", "test-supervisor");
        var review = new RecoveryReview("recovery-pending-hub", twin.AssetId, twin.StateRevision, "challenge", now.AddMinutes(5));
        var commands = new InterruptPendingResponse(CreateGateway(client), accepted.Task);
        var activity = new RecoveryActivities(new HttpEnergyReadGateway(client, NullLogger<HttpEnergyReadGateway>.Instance),
            commands, new RecoveryStageReader(now), authorization, clock);
        var request = new RecoveryRequest(twin.AssetId, twin.StateRevision, now, review.InstanceId);
        var execution = new RecoveryExecution(review.InstanceId, request, review,
            authorization.Sign(new RecoveryDecisionRequest(review, true), now));
        try
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => activity.ExecuteRecovery(execution, TestContext.Current.CancellationToken));
            var pendingTwin = await ReadTwinAsync(client);
            Assert.Equal(CommandExecutionStatus.Pending, pendingTwin.LastCommand!.Status);
            Assert.True(pendingTwin.StateRevision > twin.StateRevision);

            var redelivered = await activity.ExecuteRecovery(execution, TestContext.Current.CancellationToken);
            Assert.Equal("CommandPending", redelivered.State);
            Assert.Null(redelivered.CommandExecuted);
            Assert.Null(redelivered.Resolved);
            Assert.Equal(1, deviceCommands);
            Assert.Equal(1, commands.Attempts);

            finishDeviceCommand.SetResult();
            await commands.InFlight!;
            var reconciled = await activity.ReconcileRecovery(request, TestContext.Current.CancellationToken);
            Assert.Equal("Recovered", reconciled.State);
            Assert.True(reconciled.CommandExecuted);
            Assert.True(reconciled.Resolved);
            Assert.Equal(1, deviceCommands);
            Assert.Equal(pendingTwin.StateRevision, (await ReadTwinAsync(client)).StateRevision);
        }
        finally
        {
            finishDeviceCommand.TrySetResult();
            if (commands.InFlight is not null)
            {
                await commands.InFlight;
            }
        }
    }

    [Fact]
    public async Task DurableActivityRecoversAnAcceptedHubCommandAfterLosingItsHttpResponse()
    {
        await using var factory = CreateEnergyHub();
        using var client = factory.CreateClient();
        using var seeded = await client.PostAsJsonAsync("/api/energy/admin/scenario",
            new EnergyScenarioSyncRequest(false, null, "Forgotten override recovery test"), TestContext.Current.CancellationToken);
        seeded.EnsureSuccessStatusCode();
        var twin = await ReadTwinAsync(client);
        Assert.True(twin.IsAnomalous);

        var clock = new TestTimeProvider();
        var now = clock.GetUtcNow();
        var authorization = new RecoveryAuthorization("operator", "supervisor", "test-supervisor");
        var review = new RecoveryReview("recovery-real-hub", twin.AssetId, twin.StateRevision, "challenge", now.AddMinutes(5));
        var command = new LoseSuccessfulResponse(CreateGateway(client));
        var activity = new RecoveryActivities(new HttpEnergyReadGateway(client, NullLogger<HttpEnergyReadGateway>.Instance),
            command, new RecoveryStageReader(now), authorization, clock);
        var execution = new RecoveryExecution(review.InstanceId,
            new RecoveryRequest(twin.AssetId, twin.StateRevision, now, review.InstanceId), review,
            authorization.Sign(new RecoveryDecisionRequest(review, true), now));

        await Assert.ThrowsAsync<HttpRequestException>(() => activity.ExecuteRecovery(execution, TestContext.Current.CancellationToken));
        var acceptedRevision = (await ReadTwinAsync(client)).StateRevision;
        var recovered = await activity.ExecuteRecovery(execution, TestContext.Current.CancellationToken);
        Assert.Equal("Recovered", recovered.State);
        Assert.True(recovered.CommandExecuted);
        Assert.True(recovered.Resolved);
        Assert.Equal(1, command.Attempts);
        Assert.Equal(acceptedRevision, (await ReadTwinAsync(client)).StateRevision);
    }

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

                // Same removal as the MCP server tests: the startup hydration's background read
                // would spin retries against the deliberately failing gateway and muddy what these
                // tests pin about the command path.
                var hydration = services.FirstOrDefault(descriptor =>
                    descriptor.ImplementationType == typeof(TwinHydration));
                if (hydration is not null)
                {
                    services.Remove(hydration);
                }
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

    private sealed class RecoveryStageReader(DateTimeOffset appliedAt) : ICommandCenterStageReader
    {
        public Task<DemoStageStatus> GetCurrentStageAsync(string correlationId, CancellationToken cancellationToken) =>
            Task.FromResult(new DemoStageStatus(DemoStage.Hosting, "Hosting", "Recovery", [], appliedAt, correlationId));
    }

    private sealed class LoseSuccessfulResponse(IEnergyCommandGateway inner) : IEnergyCommandGateway
    {
        public int Attempts { get; private set; }

        public async Task<EnergyCommandOutcome> RestoreScheduledModeAsync(string assetId, string correlationId,
            long expectedStateRevision, CancellationToken cancellationToken)
        {
            Attempts++;
            var result = await inner.RestoreScheduledModeAsync(assetId, correlationId, expectedStateRevision, cancellationToken);
            Assert.Equal(CommandExecutionStatus.Succeeded, result.Status);
            throw new HttpRequestException("Hub response lost after acceptance.");
        }
    }

    private sealed class InterruptPendingResponse(IEnergyCommandGateway inner, Task accepted) : IEnergyCommandGateway
    {
        public int Attempts { get; private set; }
        public Task<EnergyCommandOutcome>? InFlight { get; private set; }

        public async Task<EnergyCommandOutcome> RestoreScheduledModeAsync(string assetId, string correlationId,
            long expectedStateRevision, CancellationToken cancellationToken)
        {
            Attempts++;
            InFlight = inner.RestoreScheduledModeAsync(assetId, correlationId, expectedStateRevision, cancellationToken);
            await accepted.WaitAsync(cancellationToken);
            throw new HttpRequestException("Worker lost its request while the Hub was still awaiting the device.");
        }
    }
}
