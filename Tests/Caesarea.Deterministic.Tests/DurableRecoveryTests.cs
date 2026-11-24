using DurableOperations.Contracts;
using OperationsAgent.Api.Services;
using OperationsAgent.Functions;

namespace Caesarea.Deterministic.Tests;

public sealed class DurableRecoveryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void OperatorCredentialCannotApprove()
    {
        var auth = Authorization();
        Assert.True(auth.IsOperator("operator-key"));
        Assert.False(auth.IsSupervisor("operator-key"));
        Assert.False(auth.IsOperator(null));
    }

    [Fact]
    public void SignedReviewBindsOperationAssetRevisionDecisionAndExpiry()
    {
        var auth = Authorization();
        var review = Review();
        var signed = auth.Sign(new RecoveryDecisionRequest(review, true), Now);
        Assert.True(auth.Verify(signed, review, Now));
        Assert.False(auth.Verify(signed with { Approved = false }, review, Now));
        Assert.False(auth.Verify(signed with { Reviewer = "another-person" }, review, Now));
        Assert.False(auth.Verify(signed, review with { InstanceId = "another-operation" }, Now));
        Assert.False(auth.Verify(signed, review with { AssetId = "L-999" }, Now));
        Assert.False(auth.Verify(signed, review with { Revision = 43 }, Now));
        Assert.False(auth.Verify(signed, review, review.ExpiresAt));
    }

    [Fact]
    public async Task LostActivityResultRecoversHubReceiptWithoutRepeatingCommand()
    {
        var city = new City { ThrowAfterAcceptance = true };
        var activity = Activity(city);
        var execution = Execution();
        await Assert.ThrowsAsync<HttpRequestException>(() => activity.ExecuteRecovery(execution, TestContext.Current.CancellationToken));
        var recovered = await activity.ExecuteRecovery(execution, TestContext.Current.CancellationToken);
        Assert.Equal("Recovered", recovered.State);
        Assert.True(recovered.CommandExecuted);
        Assert.True(recovered.Resolved);
        Assert.Equal(1, city.CommandCount);
    }

    [Fact]
    public async Task ReplayedActivityUsesFixedRevisionAndDoesNotRepeatCommand()
    {
        var city = new City();
        var activity = Activity(city);
        var execution = Execution();
        Assert.Equal("Completed", (await activity.ExecuteRecovery(execution, TestContext.Current.CancellationToken)).State);
        Assert.Equal("Recovered", (await activity.ExecuteRecovery(execution, TestContext.Current.CancellationToken)).State);
        Assert.Equal(1, city.CommandCount);
        Assert.Equal(42, city.CommandRevision);
    }

    [Fact]
    public async Task ExecutionRedeliveryWithAChangedRevisionReconcilesWithoutResending()
    {
        var city = new City();
        city.Twin = city.Twin with { StateRevision = 43, OperationContext = new OperationalContext(true, "Security operation requires lighting.") };
        var result = await Activity(city).ExecuteRecovery(Execution(), TestContext.Current.CancellationToken);
        Assert.Equal("CommandPending", result.State);
        Assert.Null(result.CommandExecuted);
        Assert.True(city.Twin.ReportedIsOn);
        Assert.Equal(0, city.CommandCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StageWithdrawalOrReplacementRevokesApproval(bool restoredStage)
    {
        var city = new City { Stage = restoredStage ? DemoStage.Hosting : DemoStage.Deterministic, StageAppliedAt = Now.AddSeconds(1) };
        var result = await Activity(city).ExecuteRecovery(Execution(), TestContext.Current.CancellationToken);
        Assert.Equal("StageWithdrawn", result.State);
        Assert.Equal(0, city.CommandCount);
    }

    [Fact]
    public async Task ExpiredDecisionCannotCommand()
    {
        var city = new City();
        var activity = Activity(city, Now.AddMinutes(6));
        Assert.Equal("ApprovalInvalid", (await activity.ExecuteRecovery(Execution(), TestContext.Current.CancellationToken)).State);
        Assert.Equal(0, city.CommandCount);
    }

    [Fact]
    public async Task ForgedEventCannotCommand()
    {
        var city = new City();
        var execution = Execution();
        execution = execution with { Decision = execution.Decision! with { Signature = "forged" } };
        Assert.Equal("ApprovalInvalid", (await Activity(city).ExecuteRecovery(execution, TestContext.Current.CancellationToken)).State);
        Assert.Equal(0, city.CommandCount);
    }

    [Fact]
    public async Task DenialIsACompletedDecisionWithoutACommand()
    {
        var city = new City();
        var execution = Execution() with { Decision = Authorization().Sign(new RecoveryDecisionRequest(Review(), false), Now) };
        Assert.Equal("Declined", (await Activity(city).ExecuteRecovery(execution, TestContext.Current.CancellationToken)).State);
        Assert.Equal(0, city.CommandCount);
    }

    [Fact]
    public async Task EvenAnAutomaticPolicyBranchRequiresReviewInTheRecoveryBeat()
    {
        var city = new City();
        city.Twin = city.Twin with { ManualOverride = false };
        var result = await Activity(city).ExecuteRecovery(Execution() with { Decision = null }, TestContext.Current.CancellationToken);
        Assert.Equal("ApprovalInvalid", result.State);
        Assert.Equal(0, city.CommandCount);
    }

    private static RecoveryAuthorization Authorization() => new("operator-key", "supervisor-key", "supervisor");
    private static RecoveryReview Review() => new("repair-test", DemoAssets.StreetlightAssetId, 42, "challenge", Now.AddMinutes(5));
    private static RecoveryExecution Execution() => new("repair-test", new RecoveryRequest(DemoAssets.StreetlightAssetId, 42, Now, "repair-test"),
        Review(), Authorization().Sign(new RecoveryDecisionRequest(Review(), true), Now));
    private static RecoveryActivities Activity(City city, DateTimeOffset? now = null) => new(city, city, city, Authorization(), new TestTimeProvider(now ?? Now));

    private sealed class City : IEnergyReadGateway, IEnergyCommandGateway, ICommandCenterStageReader
    {
        public EnergyOperationalTwin Twin { get; set; } = new(DemoAssets.StreetlightAssetId, DemoAssets.NorthPromenadeArea,
            true, false, true, false, true, ControllerHealthInfo.Healthy, null, null, false, null, Now, OperationalContext.None, 42);
        public DemoStage Stage { get; init; } = DemoStage.Hosting;
        public DateTimeOffset StageAppliedAt { get; init; } = Now;
        public bool ThrowAfterAcceptance { get; init; }
        public int CommandCount { get; private set; }
        public long CommandRevision { get; private set; }

        public Task<EnergyOperationalTwin> GetStateAsync(string assetId, string correlationId, CancellationToken cancellationToken) => Task.FromResult(Twin);
        public Task<IReadOnlyList<ActivityRecord>> GetRecentActivityAsync(string assetId, int limit, string correlationId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ActivityRecord>>([]);
        public Task<DemoStageStatus> GetCurrentStageAsync(string correlationId, CancellationToken cancellationToken) =>
            Task.FromResult(new DemoStageStatus(Stage, "stage", "test", [], StageAppliedAt, correlationId));

        public Task<EnergyCommandOutcome> RestoreScheduledModeAsync(string assetId, string correlationId, long expectedStateRevision, CancellationToken cancellationToken)
        {
            CommandCount++;
            CommandRevision = expectedStateRevision;
            Assert.Equal(Twin.StateRevision, expectedStateRevision);
            Twin = Twin with
            {
                StateRevision = expectedStateRevision + 1,
                ReportedIsOn = false,
                ManualOverride = false,
                LastCommand = new CommandRecord("Restore scheduled mode", false, CommandExecutionStatus.Succeeded, correlationId, Now, Now, "Confirmed")
            };
            if (ThrowAfterAcceptance)
            {
                throw new HttpRequestException("The Hub accepted the command, but its HTTP response was lost.");
            }

            return Task.FromResult(new EnergyCommandOutcome(CommandExecutionStatus.Succeeded, "Confirmed", false));
        }
    }
}
