using DurableOperations.Contracts;
using Energy.Contracts;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using OperationsAgent.Api.Services;

namespace OperationsAgent.Functions;

/// <summary>A durable repair process whose approval wait outlives the Functions worker.</summary>
public static class RecoveryOrchestration
{
    private static TaskOptions BoundedRetry => TaskOptions.FromRetryPolicy(new RetryPolicy(3, TimeSpan.FromSeconds(2)));
    /// <summary>The external event accepted only after the supervisor boundary authenticates and signs a decision.</summary>
    public const string DecisionEvent = "SupervisorDecision";

    /// <summary>Runs deterministic orchestration; all external I/O is isolated in activities.</summary>
    [Function(nameof(RecoverLighting))]
    public static async Task<RecoveryResult> RecoverLighting([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var request = context.GetInput<RecoveryRequest>() ?? throw new InvalidOperationException("A recovery request is required.");
        context.SetCustomStatus(new RecoveryProgress("Validating", "Reading authoritative state with bounded retries; no command has been scheduled."));
        var twin = await context.CallActivityAsync<EnergyOperationalTwin>(nameof(RecoveryActivities.ReadRecoveryState), request, BoundedRetry);
        if (twin.StateRevision != request.ExpectedRevision)
        {
            return Complete(context, new RecoveryResult("StateChanged", false, !twin.IsAnomalous, "State changed before validation; start a new operation."));
        }

        var policy = RemediationPolicy.Evaluate(twin);
        if (!policy.ActionRequired)
        {
            return Complete(context, new RecoveryResult("NothingToDo", false, true, policy.Reason));
        }

        // This recovery beat deliberately requires supervision for every write, including a
        // no-override anomaly. The earlier Stage 8 graph retains its original policy branches.
        var review = new RecoveryReview(context.InstanceId, request.AssetId, request.ExpectedRevision,
            context.NewGuid().ToString("N"), new DateTimeOffset(context.CurrentUtcDateTime.AddMinutes(5), TimeSpan.Zero));
        context.SetCustomStatus(new RecoveryProgress("AwaitingApproval", policy.Reason, review));
        var decisionTask = context.WaitForExternalEvent<RecoveryDecision>(DecisionEvent);

        while (!decisionTask.IsCompleted)
        {
            if (!await context.CallActivityAsync<bool>(nameof(RecoveryActivities.IsRecoveryStageCurrent), request, BoundedRetry))
            {
                return Complete(context, new RecoveryResult("StageWithdrawn", false, false, "The stage changed while approval was pending."));
            }

            var remaining = review.ExpiresAt.UtcDateTime - context.CurrentUtcDateTime;
            if (remaining <= TimeSpan.Zero)
            {
                return Complete(context, new RecoveryResult("TimedOut", false, false, "The supervisor approval window expired."));
            }

            using var timerCancellation = new CancellationTokenSource();
            var tick = context.CreateTimer(context.CurrentUtcDateTime.Add(remaining < TimeSpan.FromSeconds(15) ? remaining : TimeSpan.FromSeconds(15)), timerCancellation.Token);
            if (await Task.WhenAny(decisionTask, tick) == decisionTask)
            {
                // This token cancels a durable timer, never an agent proxy or external I/O.
#pragma warning disable S6966 // CancelAsync creates a non-durable task and can strand the replaying orchestrator.
                timerCancellation.Cancel();
#pragma warning restore S6966
            }
        }

        var decision = await decisionTask;
        context.SetCustomStatus(new RecoveryProgress("Revalidating", "Checking the signed decision, stage and current Hub revision."));
        // This read has no side effects and is checkpointed before execution is scheduled.
        // It can therefore report a definite no-command outcome for a stale approval.
        twin = await context.CallActivityAsync<EnergyOperationalTwin>(nameof(RecoveryActivities.ReadRecoveryState), request, BoundedRetry);
        if (twin.StateRevision != request.ExpectedRevision)
        {
            return Complete(context, new RecoveryResult("StateChanged", false, !twin.IsAnomalous,
                "The Hub revision changed before command dispatch; the approval cannot authorize this state."));
        }

        context.SetCustomStatus(new RecoveryProgress("Executing", "Execution scheduled; a lost result requires receipt reconciliation."));
        RecoveryResult result;
        try
        {
            result = await context.CallActivityAsync<RecoveryResult>(nameof(RecoveryActivities.ExecuteRecovery),
                new RecoveryExecution(context.InstanceId, request, review, decision), BoundedRetry);
        }
        catch (TaskFailedException)
        {
            // Exhausted retries may mean a lost response, not a failed physical effect.
            result = new RecoveryResult("CommandPending", null, null, "Execution did not return a durable result; reconciling without resending.");
        }

        if (result.State == "CommandPending")
        {
            result = await ReconcileAsync(context, request);
        }

        return Complete(context, result);
    }

    private static async Task<RecoveryResult> ReconcileAsync(TaskOrchestrationContext context, RecoveryRequest request)
    {
        context.SetCustomStatus(new RecoveryProgress("Reconciling", "Waiting for the matching Hub receipt. No command will be resent."));
        var deadline = context.CurrentUtcDateTime.AddSeconds(30);
        while (context.CurrentUtcDateTime < deadline)
        {
            try
            {
                var observed = await context.CallActivityAsync<RecoveryResult>(nameof(RecoveryActivities.ReconcileRecovery), request, BoundedRetry);
                if (observed.State != "CommandPending")
                {
                    return observed;
                }
            }
            catch (TaskFailedException)
            {
                // A missing read cannot establish the command's outcome. Keep the same deadline.
            }

            var next = context.CurrentUtcDateTime.AddSeconds(2);
            if (next < deadline)
            {
                await context.CreateTimer(next, CancellationToken.None);
            }
            else
            {
                break;
            }
        }

        return new RecoveryResult("OutcomeUnknown", null, null,
            "Receipt reconciliation expired without confirmation. The original command may still complete; inspect the Hub before starting further work.");
    }

    private static RecoveryResult Complete(TaskOrchestrationContext context, RecoveryResult result)
    {
        context.SetCustomStatus(new RecoveryProgress(result.State, result.Summary));
        return result;
    }
}
