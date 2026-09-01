using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OperationsAgent.Api.Services;
using OperationsAgent.Contracts;

namespace Caesarea.Deterministic.Tests;

/// <summary>
/// Boots the real Operations Agent API in process - only the model, the Energy Hub gateways and
/// the Azure credential are faked - and pins what a client actually sees at the control-point
/// boundaries: an unresolvable approval loop reported as a conflict rather than a plausible-looking
/// answer, a capacity refusal that is not disguised as a same-asset conflict, and approval requests
/// that carry which control point raised them and which capability is waiting.
/// </summary>
public sealed class OperationsAgentApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    [Fact]
    public async Task AnUnresolvableApprovalLoopIsReportedAsAConflict()
    {
        // A model that keeps re-requesting a capability exhausts the bounded rounds. Returning its
        // last answer would show the operator a conclusion whose tool calls never ran.
        await using var world = new ApiWorld
        {
            Agent = { OnAskAsync = (_, _, _) => throw new OperationsAgentApprovalLoopException(3, "create_maintenance_work_item") }
        };
        using var client = world.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/operations-agent/ask",
            new OperationsAgentRequest("Why is L-417 lit?", null),
            JsonOptions,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Contains(
            "create_maintenance_work_item",
            problem.GetProperty("detail").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnApprovalRequestCarriesItsControlPointAndCapabilityOverTheWire()
    {
        // The three control points raise the same-looking prompt, and which one it is carries the
        // lesson. The UI must be able to read that from the contract instead of guessing from the
        // message text, so the typed fields have to survive the wire.
        await using var world = new ApiWorld();
        using var client = world.CreateClient();
        var approvals = world.Services.GetRequiredService<PendingApprovalStore>();

        var (id, decision) = approvals.Create(
            "The agent selected create_maintenance_work_item (assetId: L-417). Approve running it?",
            "metadata-corr",
            CancellationToken.None,
            OperationsAgentControlPoint.ToolApproval,
            "create_maintenance_work_item",
            "assetId: L-417, summary: Controller unresponsive.");

        var pending = await client.GetFromJsonAsync<IReadOnlyList<OperationsAgentPendingApproval>>(
            "/api/operations-agent/approvals", JsonOptions, TestContext.Current.CancellationToken);

        var waiting = Assert.Single(pending!);
        Assert.Equal(id, waiting.Id);
        Assert.Equal(OperationsAgentControlPoint.ToolApproval, waiting.ControlPoint);
        Assert.Equal("create_maintenance_work_item", waiting.ToolName);
        Assert.Equal("assetId: L-417, summary: Controller unresponsive.", waiting.ToolArguments);

        using var answered = await client.PostAsJsonAsync(
            $"/api/operations-agent/approvals/{id}",
            new OperationsAgentApprovalDecision(true),
            JsonOptions,
            TestContext.Current.CancellationToken);

        answered.EnsureSuccessStatusCode();
        Assert.True(await decision);
    }

    [Fact]
    public async Task AnAskThatStartedNoWorkflowIsAnsweredAsNoContent()
    {
        // Most asks start no workflow, so this lookup misses far more often than it hits. An empty
        // 200 body is not JSON: the Command Center's client threw on it and printed the serializer
        // error on the projector. Absence has to be reported as absence.
        await using var world = new ApiWorld();
        using var client = world.CreateClient();

        using var response = await client.GetAsync(
            "/api/operations-agent/remediation/runs?correlationId=no-such-correlation",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task AStartedRunIsStillFoundByItsCorrelation()
    {
        await using var world = new ApiWorld();
        using var client = world.CreateClient();

        using var started = await StartRemediationAsync(client, "L-417");
        started.EnsureSuccessStatusCode();
        var report = await started.Content.ReadFromJsonAsync<OperationsAgentWorkflowRunReport>(
            JsonOptions, TestContext.Current.CancellationToken);

        var found = await client.GetFromJsonAsync<OperationsAgentWorkflowRunReport>(
            $"/api/operations-agent/remediation/runs?correlationId={Uri.EscapeDataString(report!.CorrelationId)}",
            JsonOptions,
            TestContext.Current.CancellationToken);

        Assert.Equal(report.RunId, found!.RunId);

        await DenyEveryPendingApprovalAsync(client, waitFor: 1);
        await WaitForCompletionAsync(client, report.RunId);
    }

    [Fact]
    public async Task CapacityIsRefusedAsCapacityAndNotAsASameAssetConflict()
    {
        // Two different refusals: this asset is already being remediated, and this service is
        // already running as many remediations as it admits. A caller told "wait for that run to
        // finish" when the truth is "come back later" retries against the wrong condition.
        await using var world = new ApiWorld();
        using var client = world.CreateClient();
        string[] assets = ["L-401", "L-402", "L-403", "L-404", "L-405", "L-406", "L-407", "L-408"];
        List<string> runIds = [];

        foreach (var asset in assets)
        {
            using var started = await StartRemediationAsync(client, asset);
            started.EnsureSuccessStatusCode();
            var report = await started.Content.ReadFromJsonAsync<OperationsAgentWorkflowRunReport>(
                JsonOptions, TestContext.Current.CancellationToken);
            runIds.Add(report!.RunId);
        }

        await WaitForPendingApprovalsAsync(client, assets.Length);

        using var sameAsset = await StartRemediationAsync(client, "L-401");
        Assert.Equal(HttpStatusCode.Conflict, sameAsset.StatusCode);
        var conflict = await sameAsset.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Contains("already in progress", conflict.GetProperty("title").GetString(), StringComparison.Ordinal);

        using var overCapacity = await StartRemediationAsync(client, "L-409");
        Assert.Equal(HttpStatusCode.TooManyRequests, overCapacity.StatusCode);
        var capacity = await overCapacity.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Contains("capacity reached", capacity.GetProperty("title").GetString(), StringComparison.OrdinalIgnoreCase);

        // Capacity is a queue, not a wall: once the runs finish, the ninth is admitted.
        await DenyEveryPendingApprovalAsync(client);

        foreach (var runId in runIds)
        {
            await WaitForCompletionAsync(client, runId);
        }

        using var admitted = await StartRemediationAsync(client, "L-409");
        admitted.EnsureSuccessStatusCode();
        var ninth = await admitted.Content.ReadFromJsonAsync<OperationsAgentWorkflowRunReport>(
            JsonOptions, TestContext.Current.CancellationToken);

        // Answer and drain it too: a run still in flight when the host stops logs its cancellation
        // into a logger that is already gone.
        await DenyEveryPendingApprovalAsync(client, waitFor: 1);
        await WaitForCompletionAsync(client, ninth!.RunId);
    }

    private static Task<HttpResponseMessage> StartRemediationAsync(HttpClient client, string assetId) =>
        client.PostAsJsonAsync(
            "/api/operations-agent/remediation",
            new OperationsAgentRemediationRequest(assetId),
            JsonOptions,
            TestContext.Current.CancellationToken);

    private static async Task<IReadOnlyList<OperationsAgentPendingApproval>> WaitForPendingApprovalsAsync(HttpClient client, int count)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var pending = await client.GetFromJsonAsync<IReadOnlyList<OperationsAgentPendingApproval>>(
                "/api/operations-agent/approvals", JsonOptions, TestContext.Current.CancellationToken) ?? [];

            if (pending.Count >= count)
            {
                return pending;
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"Only fewer than {count} approvals were parked in time.");
    }

    private static async Task DenyEveryPendingApprovalAsync(HttpClient client, int waitFor = 0)
    {
        var pending = waitFor > 0
            ? await WaitForPendingApprovalsAsync(client, waitFor)
            : await client.GetFromJsonAsync<IReadOnlyList<OperationsAgentPendingApproval>>(
                "/api/operations-agent/approvals", JsonOptions, TestContext.Current.CancellationToken) ?? [];

        foreach (var approval in pending)
        {
            using var response = await client.PostAsJsonAsync(
                $"/api/operations-agent/approvals/{approval.Id}",
                new OperationsAgentApprovalDecision(false),
                JsonOptions,
                TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
        }
    }

    private static async Task WaitForCompletionAsync(HttpClient client, string runId)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var report = await client.GetFromJsonAsync<OperationsAgentWorkflowRunReport>(
                $"/api/operations-agent/remediation/runs/{runId}", JsonOptions, TestContext.Current.CancellationToken);

            if (report is { Completed: true })
            {
                return;
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"Remediation run {runId} did not complete in time.");
    }

    /// <summary>
    /// The real service with its outside world replaced: no model, no Energy Hub, no Azure
    /// credential chain, and no stage synchronizer polling a Command Center that is not there.
    /// </summary>
    private sealed class ApiWorld : IAsyncDisposable
    {
        private readonly WebApplicationFactory<FoundryOperationsAgent> _factory;

        public ApiWorld()
        {
            _factory = new WebApplicationFactory<FoundryOperationsAgent>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IOperationsAgent>();
                    services.AddSingleton<IOperationsAgent>(Agent);
                    services.RemoveAll<DemoStageGate>();
                    services.AddSingleton(new DemoStageGate(DemoStage.ToolApproval));
                    services.RemoveAll<TokenCredential>();
                    services.AddSingleton<TokenCredential>(new StubTokenCredential());
                    services.RemoveAll<IEnergyReadGateway>();
                    services.AddSingleton<IEnergyReadGateway>(new FakeEnergyReadGateway
                    {
                        State = CreateOverriddenTwin("L-417"),
                        Activity = [],
                        OnGetStateAsync = (assetId, _, _) => Task.FromResult(CreateOverriddenTwin(assetId))
                    });
                    services.RemoveAll<IEnergyCommandGateway>();
                    services.AddSingleton<IEnergyCommandGateway>(new FakeEnergyCommandGateway());
                    var synchronizer = services.FirstOrDefault(descriptor =>
                        descriptor.ImplementationType == typeof(DemoStageSynchronizer));

                    if (synchronizer is not null)
                    {
                        services.Remove(synchronizer);
                    }
                });
            });
        }

        public FakeOperationsAgent Agent { get; } = new();

        public IServiceProvider Services => _factory.Services;

        public HttpClient CreateClient() => _factory.CreateClient();

        public async ValueTask DisposeAsync()
        {
            // Any run still parked on an unanswered approval is released before the host stops.
            Services.GetRequiredService<RemediationWorkflowService>().CancelActiveRuns();
            await _factory.DisposeAsync();
        }

        private static EnergyOperationalTwin CreateOverriddenTwin(string assetId) => new(
            assetId,
            DemoAssets.NorthPromenadeArea,
            ReportedIsOn: true,
            DesiredIsOn: true,
            IsDaylight: true,
            ExpectedScheduledState: false,
            ManualOverride: true,
            ControllerHealthInfo.Healthy,
            LastCommand: null,
            LastMaintenanceTime: null,
            HasRecentMaintenance: false,
            OpenIncidentId: null,
            LastReportedAt: DateTimeOffset.UtcNow,
            OperationalContext.None,
            StateRevision: 1);
    }
}

internal sealed class FakeOperationsAgent : IOperationsAgent
{
    public Func<string, string?, string, Task<OperationsAgentAnswer>>? OnAskAsync { get; set; }

    public Task<OperationsAgentAnswer> AskAsync(
        string question, string? sessionId, string correlationId, CancellationToken cancellationToken) =>
        OnAskAsync is not null
            ? OnAskAsync(question, sessionId, correlationId)
            : Task.FromResult(new OperationsAgentAnswer(
                "L-417 is lit under a manual override.",
                sessionId ?? "session-1",
                [],
                [],
                [],
                [],
                OperationsAgentToolSource.Local,
                1));
}

/// <summary>
/// Stands in for the Azure credential chain: the test must never spawn the CLI and PowerShell
/// probes DefaultAzureCredential performs, and no request under test reaches Foundry.
/// </summary>
internal sealed class StubTokenCredential : TokenCredential
{
    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        new("stub-token", DateTimeOffset.UtcNow.AddHours(1));

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        ValueTask.FromResult(GetToken(requestContext, cancellationToken));
}
