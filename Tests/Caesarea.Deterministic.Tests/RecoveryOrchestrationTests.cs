using DurableOperations.Contracts;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using OperationsAgent.Functions;

namespace Caesarea.Deterministic.Tests;

public sealed class RecoveryOrchestrationTests
{
    [Theory]
    [InlineData(nameof(RecoveryActivities.ReadRecoveryState))]
    [InlineData(nameof(RecoveryActivities.IsRecoveryStageCurrent))]
    public async Task TransientReadFailureRecoversAndStillRequiresApproval(string failingActivity)
    {
        var context = new RecoveryContext { FailingActivity = failingActivity, FailuresRemaining = 1 };
        var result = await RecoveryOrchestration.RecoverLighting(context);
        Assert.Equal("Completed", result.State);
        Assert.True(context.ApprovalWasRequested);
        Assert.Equal(1, context.FailuresObserved);
        Assert.Equal(1, context.Executions);
        Assert.All(context.RetryLimits, limit => Assert.Equal(3, limit));
    }

    [Theory]
    [InlineData(nameof(RecoveryActivities.ReadRecoveryState), "Validating")]
    [InlineData(nameof(RecoveryActivities.IsRecoveryStageCurrent), "AwaitingApproval")]
    public async Task ExhaustedReadsFailBeforeDispatchWithARetryablePhase(string failingActivity, string expectedPhase)
    {
        var context = new RecoveryContext { FailingActivity = failingActivity, FailuresRemaining = 10 };
        await Assert.ThrowsAsync<TaskFailedException>(() => RecoveryOrchestration.RecoverLighting(context));
        Assert.Equal(3, context.FailuresObserved);
        Assert.Equal(expectedPhase, context.Progress!.State);
        Assert.Equal(0, context.Executions);
    }

    [Fact]
    public async Task PendingReceiptIsPolledWithoutExecutingAgain()
    {
        var context = new RecoveryContext { ReturnPending = true, PendingReads = 2 };
        var result = await RecoveryOrchestration.RecoverLighting(context);
        Assert.Equal("Recovered", result.State);
        Assert.True(result.CommandExecuted);
        Assert.Equal(1, context.Executions);
        Assert.Equal(3, context.ReconciliationReads);
    }

    [Fact]
    public async Task PendingReceiptThatNeverCompletesBecomesUnknownNotFalse()
    {
        var context = new RecoveryContext { ReturnPending = true, PendingReads = 100 };
        var result = await RecoveryOrchestration.RecoverLighting(context);
        Assert.Equal("OutcomeUnknown", result.State);
        Assert.Null(result.CommandExecuted);
        Assert.Null(result.Resolved);
        Assert.Equal(1, context.Executions);
        Assert.InRange(context.ReconciliationReads, 1, 16);
    }

    [Fact]
    public async Task ExhaustedExecutionAndReconciliationReadsCannotReportNoCommand()
    {
        var context = new RecoveryContext { FailExecutionAndReconciliation = true };
        var result = await RecoveryOrchestration.RecoverLighting(context);
        Assert.Equal("OutcomeUnknown", result.State);
        Assert.Null(result.CommandExecuted);
        Assert.Equal(3, context.Executions);
    }

    [Fact]
    public async Task StaleApprovalIsRefusedBeforeExecutionIsScheduled()
    {
        var context = new RecoveryContext { ChangeRevisionAfterApproval = true };
        var result = await RecoveryOrchestration.RecoverLighting(context);
        Assert.Equal("StateChanged", result.State);
        Assert.False(result.CommandExecuted);
        Assert.Equal(0, context.Executions);
    }

    // Executes the production orchestrator against a deterministic scheduler double. The double
    // honors ONLY the retry policy supplied by the orchestrator, so a missing policy fails these
    // tests. This tests our scheduling/branching; it is not a substitute for a DTS integration run.
    private sealed class RecoveryContext : TaskOrchestrationContext
    {
        private static readonly DateTimeOffset StartedAt = new(2026, 11, 17, 10, 0, 0, TimeSpan.Zero);
        private readonly TaskCompletionSource<RecoveryDecision> _decision = new();
        private DateTime _now = StartedAt.UtcDateTime;
        public string? FailingActivity { get; init; }
        public int FailuresRemaining { get; set; }
        public int FailuresObserved { get; private set; }
        public bool ReturnPending { get; init; }
        public int PendingReads { get; set; }
        public bool FailExecutionAndReconciliation { get; init; }
        public bool ChangeRevisionAfterApproval { get; init; }
        public int Executions { get; private set; }
        public int ReconciliationReads { get; private set; }
        public bool ApprovalWasRequested { get; private set; }
        public List<int> RetryLimits { get; } = [];
        public RecoveryProgress? Progress { get; private set; }
        public override TaskName Name => nameof(RecoveryOrchestration.RecoverLighting);
        public override string InstanceId => "recovery-orchestration-test";
        public override ParentOrchestrationInstance? Parent => null;
        public override DateTime CurrentUtcDateTime => _now;
        public override bool IsReplaying => false;
        protected override ILoggerFactory LoggerFactory => NullLoggerFactory.Instance;
        public override T? GetInput<T>() where T : default => (T)(object)new RecoveryRequest("L-417", 42, StartedAt, InstanceId);

        public override Task<T> CallActivityAsync<T>(TaskName name, object? input = null, TaskOptions? options = null)
        {
            var limit = options?.Retry?.Policy?.MaxNumberOfAttempts ?? 1;
            RetryLimits.Add(limit);
            for (var attempt = 1; attempt <= limit; attempt++)
            {
                try
                {
                    return Task.FromResult((T)InvokeActivity(name.Name));
                }
                catch (HttpRequestException exception)
                {
                    if (attempt == limit)
                    {
                        throw new TaskFailedException(name.Name, 0, exception);
                    }

                    _now = _now.Add(options!.Retry!.Policy!.FirstRetryInterval);
                }
            }

            throw new InvalidOperationException("A positive retry limit is required.");
        }

        private object InvokeActivity(string name)
        {
            if (name == FailingActivity && FailuresRemaining-- > 0)
            {
                FailuresObserved++;
                throw new HttpRequestException("Transient boundary read failure.");
            }

            switch (name)
            {
                case nameof(RecoveryActivities.ReadRecoveryState):
                    return new EnergyOperationalTwin("L-417", "Promenade", true, false, true, false, true,
                        ControllerHealthInfo.Healthy, null, null, false, null, StartedAt, OperationalContext.None,
                        ChangeRevisionAfterApproval && _decision.Task.IsCompleted ? 43 : 42);
                case nameof(RecoveryActivities.IsRecoveryStageCurrent):
                    return true;
                case nameof(RecoveryActivities.ExecuteRecovery):
                    Executions++;
                    if (FailExecutionAndReconciliation) { throw new HttpRequestException("Lost execution result."); }
                    Assert.True(_decision.Task.IsCompleted);
                    return ReturnPending ? new RecoveryResult("CommandPending", null, null, "Pending")
                        : new RecoveryResult("Completed", true, true, "Completed");
                case nameof(RecoveryActivities.ReconcileRecovery):
                    ReconciliationReads++;
                    if (FailExecutionAndReconciliation) { throw new HttpRequestException("Receipt read unavailable."); }
                    return PendingReads-- > 0 ? new RecoveryResult("CommandPending", null, null, "Pending")
                        : new RecoveryResult("Recovered", true, true, "Recovered");
                default:
                    throw new InvalidOperationException($"Unexpected activity: {name}");
            }
        }

        public override Task CreateTimer(DateTime fireAt, CancellationToken cancellationToken)
        {
            _now = fireAt;
            if (!_decision.Task.IsCompleted && Progress?.Review is { } review)
            {
                _decision.SetResult(new RecoveryDecision(review, true, "test-supervisor", new DateTimeOffset(_now), "test-signature"));
            }

            return Task.CompletedTask;
        }

        public override Task<T> WaitForExternalEvent<T>(string eventName, CancellationToken cancellationToken = default)
        {
            Assert.Equal(RecoveryOrchestration.DecisionEvent, eventName);
            ApprovalWasRequested = true;
            return (Task<T>)(object)_decision.Task;
        }

        public override void SetCustomStatus(object? customStatus) => Progress = (RecoveryProgress?)customStatus;
        public override Guid NewGuid() => Guid.Parse("16b35615-b1f0-4de6-b9c6-2283a2ac2366");
        public override void SendEvent(string instanceId, string eventName, object payload) => throw new NotSupportedException();
        public override Task<T> CallSubOrchestratorAsync<T>(TaskName orchestratorName, object? input = null, TaskOptions? options = null) => throw new NotSupportedException();
        public override void ContinueAsNew(object? newInput = null, bool preserveUnprocessedEvents = true) => throw new NotSupportedException();
    }
}
