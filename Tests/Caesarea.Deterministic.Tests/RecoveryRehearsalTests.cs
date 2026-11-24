using System.Net;
using System.Text.Json;
using DemoControl.Web.Services;
using DurableOperations.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Caesarea.Deterministic.Tests;

public sealed class RecoveryRehearsalTests
{
    [Fact]
    public async Task PreparationStartsInfrastructureButNoConversationOrRepair()
    {
        using var fixture = new RehearsalFixture();
        await fixture.Controller.PrepareAsync();
        Assert.True(fixture.Controller.State.Prepared);
        Assert.Null(fixture.Controller.State.SessionId);
        Assert.Empty(fixture.Api.Turns);
        Assert.Equal(1, fixture.Runtime.Starts);
    }

    [Fact]
    public async Task MainAppCannotStartWithoutPreparedFoundation()
    {
        using var fixture = new RehearsalFixture();
        await fixture.Controller.BeginAsync();
        Assert.True(fixture.Controller.State.Error);
        Assert.Empty(fixture.Api.Turns);
        Assert.Equal(0, fixture.Runtime.Starts);
    }

    [Fact]
    public async Task MainAppAdapterRejectsMissingCredentialsAndOperatorApproval()
    {
        using var fixture = new RehearsalFixture();
        await fixture.WriteSettingsAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(fixture.Settings);
        builder.Services.AddSingleton(fixture.Controller);
        await using var app = builder.Build();
        app.MapRecoveryDemo();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var http = app.GetTestClient();
        using var missing = await http.GetAsync("/api/recovery-demo/status", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        http.DefaultRequestHeaders.Add("X-Caesarea-Key", new string('o', 32));
        http.DefaultRequestHeaders.Add("X-Correlation-ID", "main-app-test");
        using var status = await http.GetAsync("/api/recovery-demo/status", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.Equal("main-app-test", status.Headers.GetValues("X-Correlation-ID").Single());
        using var decision = await http.PostAsJsonAsync("/api/recovery-demo/decision", new RecoveryDecisionRequest(fixture.Api.Review, true), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, decision.StatusCode);
        Assert.Equal(0, fixture.Api.Decisions);
    }

    [Fact]
    public async Task HttpBoundarySeparatesSupervisorAuthorityAndPropagatesCorrelation()
    {
        using var fixture = new RehearsalFixture();
        var privateDirectory = Path.Combine(fixture.Settings.Root, "Services", "OperationsAgent.Functions");
        Directory.CreateDirectory(privateDirectory);
        var operatorKey = new string('o', 32);
        var supervisorKey = new string('s', 32);
        await File.WriteAllTextAsync(Path.Combine(privateDirectory, "local.settings.json"), JsonSerializer.Serialize(new
        {
            Values = new Dictionary<string, string>
            {
                ["CAESAREA_LOCAL_REHEARSAL"] = "true",
                ["DURABLE_TASK_SCHEDULER_CONNECTION_STRING"] = "Endpoint=http://localhost:18080;TaskHub=caesarea-recovery-v2;Authentication=None",
                ["ENERGYHUB_BASE_URI"] = "http://localhost:5016",
                ["COMMANDCENTER_BASE_URI"] = "http://localhost:5235",
                ["CAESAREA_OPERATOR_KEY"] = operatorKey,
                ["CAESAREA_SUPERVISOR_KEY"] = supervisorKey
            }
        }), TestContext.Current.CancellationToken);
        using var handler = new CredentialHandler();
        var factory = new TestClientFactory(handler);
        using var city = new HttpClient();
        var api = new RecoveryRehearsalApi(factory, fixture.Settings, new DemoStageApiClient(city), new DemoScenarioApiClient(city));
        await api.RunAsync("retained-session", "Check progress", TestContext.Current.CancellationToken);
        await api.DecideAsync(fixture.Api.Review, true, TestContext.Current.CancellationToken);
        Assert.Equal([operatorKey, supervisorKey], handler.Keys);
        Assert.All(handler.Correlations, value => Assert.False(string.IsNullOrWhiteSpace(value)));
        Assert.Equal("retained-session", handler.Turn!.SessionId);
        Assert.Equal(fixture.Api.Review, handler.Decision!.Review);
        Assert.DoesNotContain(supervisorKey, handler.Bodies[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task GuidedFlowRetainsReviewAndSessionAndVerifiesHub()
    {
        using var fixture = new RehearsalFixture();
        var controller = fixture.Controller;
        await controller.PrepareAsync();
        await controller.BeginAsync();
        Assert.True(controller.State.CanDecide);
        Assert.True(controller.State.BlocksCityChanges);
        var review = controller.State.Operation!.Progress!.Review!;

        await controller.CrashAndRecoverAsync();
        Assert.Single(fixture.Api.Turns);
        await controller.ResumeAsync();
        Assert.True(controller.State.Recovered);
        Assert.Equal(review, controller.State.Operation.Progress.Review);
        Assert.Equal(1, fixture.Runtime.Crashes);
        Assert.Equal(2, fixture.Api.Turns.Count);
        Assert.All(fixture.Api.Turns, turn => Assert.Equal("session-1", turn.SessionId));
        Assert.DoesNotContain("repair-1", fixture.Api.Turns[1].Message, StringComparison.Ordinal);

        await controller.DecideAsync(review, true);
        Assert.True(controller.State.Terminal);
        Assert.True(controller.State.Result!.Resolved);
        Assert.NotNull(controller.State.Hub);
        Assert.False(controller.State.BlocksCityChanges);
        Assert.Equal(1, fixture.Api.Decisions);
        Assert.Contains(controller.State.Events, item => item.Summary.Contains("Worker stopped", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StartCannotReplacePendingOperation()
    {
        using var fixture = new RehearsalFixture();
        await fixture.Controller.PrepareAsync();
        await fixture.Controller.BeginAsync();
        await fixture.Controller.PrepareAsync();
        await fixture.Controller.BeginAsync();
        Assert.Equal(1, fixture.Api.Starts);
        Assert.Equal("repair-1", fixture.Controller.State.OperationId);
        Assert.True(fixture.Controller.State.Error);
    }

    [Theory]
    [InlineData("Validating", false)]
    [InlineData("AwaitingApproval", true)]
    public async Task CrashRequiresAnUnexpiredPendingReview(string phase, bool expired)
    {
        using var fixture = new RehearsalFixture();
        await fixture.Controller.PrepareAsync();
        await fixture.Controller.BeginAsync();
        fixture.Api.Status = fixture.Api.Status with { Progress = new(phase, "test", expired ? fixture.Api.Review with { ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1) } : null) };
        await fixture.Controller.CrashAndRecoverAsync();
        Assert.Equal(0, fixture.Runtime.Crashes);
        Assert.False(fixture.Controller.State.Recovered);
        Assert.True(fixture.Controller.State.Error);
    }

    [Fact]
    public async Task ChangedReviewCannotBeClaimedAsRecoveredOrApproved()
    {
        using var fixture = new RehearsalFixture();
        await fixture.Controller.PrepareAsync();
        await fixture.Controller.BeginAsync();
        var displayed = fixture.Api.Review;
        fixture.Api.ChangeReviewOnResume = true;
        await fixture.Controller.CrashAndRecoverAsync();
        await fixture.Controller.ResumeAsync();
        Assert.False(fixture.Controller.State.Recovered);
        await fixture.Controller.DecideAsync(displayed, true);
        Assert.Equal(0, fixture.Api.Decisions);
    }

    [Fact]
    public async Task LostModelResponseKeepsSessionAndFindsOperationWithoutResending()
    {
        using var fixture = new RehearsalFixture();
        fixture.Api.LoseInitialResponse = true;
        await fixture.Controller.PrepareAsync();
        await fixture.Controller.BeginAsync();
        Assert.Equal("session-1", fixture.Controller.State.SessionId);
        Assert.True(fixture.Controller.State.BlocksCityChanges);
        await fixture.Controller.ReconnectAsync();
        Assert.Equal("repair-1", fixture.Controller.State.OperationId);
        Assert.Single(fixture.Api.Turns);
        Assert.True(fixture.Controller.State.CanDecide);
    }

    [Fact]
    public async Task DeclineFinishesWithoutReportingARepair()
    {
        using var fixture = new RehearsalFixture();
        await fixture.Controller.PrepareAsync();
        await fixture.Controller.BeginAsync();
        await fixture.Controller.DecideAsync(fixture.Api.Review, false);
        Assert.Equal("Declined", fixture.Controller.State.Result!.State);
        Assert.False(fixture.Controller.State.Result.CommandExecuted);
        Assert.False(fixture.Controller.State.Result.Resolved);
    }

    [Fact]
    public async Task UnknownOutcomeBlocksAnotherStart()
    {
        using var fixture = new RehearsalFixture();
        await fixture.Controller.PrepareAsync();
        await fixture.Controller.BeginAsync();
        fixture.Api.Status = fixture.Api.Status with
        {
            RuntimeStatus = "Completed",
            Outcome = JsonSerializer.Serialize(new RecoveryResult("OutcomeUnknown", null, null, "Inspect Hub"))
        };
        await fixture.Controller.ReconnectAsync();
        Assert.Null(fixture.Controller.State.Result!.CommandExecuted);
        Assert.True(fixture.Controller.State.BlocksCityChanges);
        await fixture.Controller.PrepareAsync();
        await fixture.Controller.BeginAsync();
        Assert.Equal(1, fixture.Api.Starts);
    }

    [Theory]
    [InlineData("Terminated", "Terminated by user.")]
    [InlineData("Failed", "Activity failed.")]
    [InlineData("Canceled", "Canceled by user.")]
    [InlineData("Completed", "Invalid business output")]
    [InlineData("Completed", "{}")]
    public async Task TerminalStatusSurvivesNonBusinessOutput(string runtimeStatus, string output)
    {
        using var fixture = new RehearsalFixture();
        await fixture.Controller.PrepareAsync();
        await fixture.Controller.BeginAsync();
        await fixture.Controller.CrashAndRecoverAsync();
        fixture.Api.Status = fixture.Api.Status with { RuntimeStatus = runtimeStatus, Outcome = output };

        await fixture.Controller.ReconnectAsync();

        var state = fixture.Controller.State;
        Assert.False(state.Error);
        Assert.True(state.Terminal);
        Assert.Equal(runtimeStatus, state.Operation!.RuntimeStatus);
        Assert.Equal(output, state.Operation.Outcome);
        Assert.Null(state.Result);
        Assert.NotNull(state.Hub);
        Assert.False(state.CanDecide);
        Assert.NotNull(state.ContinueBlockedReason);
        Assert.True(state.BlocksCityChanges);

        await fixture.Controller.ResumeAsync();
        Assert.Single(fixture.Api.Turns);
        Assert.False(fixture.Controller.State.Recovered);
        await fixture.Controller.PrepareAsync();
        Assert.Equal(1, fixture.Api.Starts);
    }

    [Fact]
    public async Task ContinuationRefreshesStatusBeforeSendingAnotherTurn()
    {
        using var fixture = new RehearsalFixture();
        await fixture.Controller.PrepareAsync();
        await fixture.Controller.BeginAsync();
        Assert.Contains("Crash worker and restart", fixture.Controller.State.ContinueBlockedReason);
        await fixture.Controller.CrashAndRecoverAsync();
        Assert.Null(fixture.Controller.State.ContinueBlockedReason);
        fixture.Api.Status = fixture.Api.Status with { RuntimeStatus = "Terminated", Outcome = "Terminated by user." };

        await fixture.Controller.ResumeAsync();

        Assert.True(fixture.Controller.State.Terminal);
        Assert.Contains("terminated", fixture.Controller.State.ContinueBlockedReason);
        Assert.Single(fixture.Api.Turns);
        Assert.True(fixture.Controller.State.Error);
    }

    [Fact]
    public async Task RetainedStateSurvivesControllerRecreation()
    {
        using var fixture = new RehearsalFixture();
        await fixture.Controller.PrepareAsync();
        await fixture.Controller.BeginAsync();
        using var restored = new RecoveryRehearsal(fixture.Runtime, fixture.Api, fixture.Settings, NullLogger<RecoveryRehearsal>.Instance);
        Assert.Equal(fixture.Controller.State.SessionId, restored.State.SessionId);
        Assert.Equal(fixture.Controller.State.OperationId, restored.State.OperationId);
        Assert.True(restored.State.BlocksCityChanges);
        await restored.ReconnectAsync();
        Assert.Single(fixture.Api.Turns);
    }

    [Fact]
    public async Task SnapshotReplacementToleratesATemporaryReaderLock()
    {
        using var fixture = new RehearsalFixture();
        await fixture.Controller.PrepareAsync();
        var path = Path.Combine(fixture.Settings.EvidenceDirectory, "rehearsal.json");
        await using var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var begin = Task.Run(fixture.Controller.BeginAsync, TestContext.Current.CancellationToken);
        await Task.Delay(250, TestContext.Current.CancellationToken);
        await reader.DisposeAsync();
        await begin;
        Assert.False(fixture.Controller.State.Error);
        Assert.True(fixture.Controller.State.CanDecide);
        Assert.Single(fixture.Api.Turns);
        using var persisted = JsonDocument.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal("session-1", persisted.RootElement.GetProperty("sessionId").GetString());
    }

    [Fact]
    public async Task LostDecisionResponseIsNotResubmitted()
    {
        using var fixture = new RehearsalFixture();
        await fixture.Controller.PrepareAsync();
        await fixture.Controller.BeginAsync();
        fixture.Api.LoseDecisionResponse = true;
        await fixture.Controller.DecideAsync(fixture.Api.Review, true);
        Assert.True(fixture.Controller.State.DecisionQueued);
        await fixture.Controller.DecideAsync(fixture.Api.Review, true);
        Assert.Equal(1, fixture.Api.Decisions);
        await fixture.Controller.ReconnectAsync();
        Assert.True(fixture.Controller.State.Terminal);
    }

    [Fact]
    public async Task ProductionNeverStartsWorker()
    {
        using var fixture = new RehearsalFixture("Production");
        await fixture.Controller.PrepareAsync();
        await fixture.Controller.BeginAsync();
        Assert.Equal(0, fixture.Runtime.Starts);
        Assert.Null(fixture.Controller.State.SessionId);
    }

    private sealed class RehearsalFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "caesarea-recovery-tests", Guid.NewGuid().ToString("N"));
        public FakeRuntime Runtime { get; } = new();
        public FakeApi Api { get; } = new();
        public RecoveryLocalSettings Settings { get; }
        public RecoveryRehearsal Controller { get; }
        public RehearsalFixture(string environment = "Development")
        {
            Directory.CreateDirectory(_root);
            Settings = new RecoveryLocalSettings(new TestEnvironment(_root, environment));
            Controller = new RecoveryRehearsal(Runtime, Api, Settings, NullLogger<RecoveryRehearsal>.Instance);
        }

        public async Task WriteSettingsAsync()
        {
            var directory = Path.Combine(Settings.Root, "Services", "OperationsAgent.Functions");
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, "local.settings.json"), JsonSerializer.Serialize(new
            {
                Values = new Dictionary<string, string>
                {
                    ["CAESAREA_LOCAL_REHEARSAL"] = "true",
                    ["DURABLE_TASK_SCHEDULER_CONNECTION_STRING"] = "Endpoint=http://localhost:18080;TaskHub=caesarea-recovery-v2;Authentication=None",
                    ["ENERGYHUB_BASE_URI"] = "http://localhost:5016",
                    ["COMMANDCENTER_BASE_URI"] = "http://localhost:5235",
                    ["CAESAREA_OPERATOR_KEY"] = new string('o', 32),
                    ["CAESAREA_SUPERVISOR_KEY"] = new string('s', 32)
                }
            }), TestContext.Current.CancellationToken);
        }

        public void Dispose()
        {
            Controller.Dispose();
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class TestEnvironment(string root, string environment) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environment;
        public string ApplicationName { get; set; } = "RecoveryTests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class FakeRuntime : IRecoveryLocalRuntime
    {
        public int Starts { get; private set; }
        public int Crashes { get; private set; }
        public int? ProcessId => Starts == 0 ? null : Starts;
        public Task EnsureRunningAsync(CancellationToken cancellationToken) { Starts++; return Task.CompletedTask; }
        public Task CrashAsync(CancellationToken cancellationToken) { Crashes++; return Task.CompletedTask; }
    }

    private sealed class TestClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class CredentialHandler : HttpMessageHandler
    {
        public List<string> Keys { get; } = [];
        public List<string> Correlations { get; } = [];
        public List<string> Bodies { get; } = [];
        public DurableAgentTurn? Turn { get; private set; }
        public RecoveryDecisionRequest? Decision { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Keys.Add(request.Headers.GetValues("X-Caesarea-Key").Single());
            Correlations.Add(request.Headers.GetValues("X-Correlation-ID").Single());
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            if (request.RequestUri!.AbsolutePath.EndsWith("/run", StringComparison.Ordinal))
            {
                Turn = await request.Content.ReadFromJsonAsync<DurableAgentTurn>(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new DurableAgentReply("Actual reply", "retained-session")) };
            }

            Decision = await request.Content.ReadFromJsonAsync<RecoveryDecisionRequest>(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Accepted) { Content = JsonContent.Create(new { state = "DecisionQueued" }) };
        }
    }

    private sealed class FakeApi : IRecoveryRehearsalApi
    {
        public RecoveryReview Review { get; } = new("repair-1", "L-417", 42, "challenge", DateTimeOffset.UtcNow.AddMinutes(5));
        public RecoveryOperationStatus Status { get; set; }
        public List<DurableAgentTurn> Turns { get; } = [];
        public int Starts { get; private set; }
        public int Decisions { get; private set; }
        public bool ChangeReviewOnResume { get; set; }
        public bool LoseInitialResponse { get; set; }
        public bool LoseDecisionResponse { get; set; }
        public FakeApi() => Status = new("repair-1", "Running", new("AwaitingApproval", "Review required", Review), null);
        public Task PrepareCityAsync(CancellationToken cancellationToken) { Starts++; return Task.CompletedTask; }
        public Task<string> CreateSessionAsync(CancellationToken cancellationToken) => Task.FromResult("session-1");
        public Task<DurableAgentReply> RunAsync(string sessionId, string message, CancellationToken cancellationToken)
        {
            Turns.Add(new(message, sessionId));
            if (Turns.Count == 1 && LoseInitialResponse) { throw new HttpRequestException("Response lost"); }
            if (Turns.Count > 1 && ChangeReviewOnResume)
            {
                Status = Status with { Progress = new("AwaitingApproval", "Changed", Review with { Revision = 43 }) };
            }

            return Task.FromResult(new DurableAgentReply("Read actual status", sessionId));
        }
        public Task<IReadOnlyList<string>> FindOperationsAsync(string sessionId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>(["repair-1"]);
        public Task<RecoveryOperationStatus> GetAsync(string operationId, CancellationToken cancellationToken) => Task.FromResult(Status);
        public Task DecideAsync(RecoveryReview review, bool approved, CancellationToken cancellationToken)
        {
            Decisions++;
            Status = Status with { RuntimeStatus = "Completed", Outcome = JsonSerializer.Serialize(new RecoveryResult(approved ? "Succeeded" : "Declined", approved, approved, "Decision applied")) };
            if (LoseDecisionResponse) { throw new HttpRequestException("Decision response lost"); }
            return Task.CompletedTask;
        }
        public Task<EnergyOperationalTwin> ReadHubAsync(CancellationToken cancellationToken) => Task.FromResult(new EnergyOperationalTwin(
            DemoAssets.StreetlightAssetId, DemoAssets.NorthPromenadeArea, false, false, true, false, false,
            ControllerHealthInfo.Healthy, null, null, false, null, DateTimeOffset.UtcNow, OperationalContext.None, 43));
    }
}
