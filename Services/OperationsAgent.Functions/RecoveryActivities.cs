using Caesarea.CanonicalModel;
using DemoScenario.Contracts;
using DurableOperations.Contracts;
using Energy.Contracts;
using Microsoft.Azure.Functions.Worker;
using OperationsAgent.Api.Services;

namespace OperationsAgent.Functions;

/// <summary>Deterministic activities own every city read and command; replaying orchestration code performs no I/O.</summary>
public sealed class RecoveryActivities
{
    private readonly IEnergyReadGateway _reads;
    private readonly IEnergyCommandGateway _commands;
    private readonly ICommandCenterStageReader _stages;
    private readonly RecoveryAuthorization _authorization;
    private readonly TimeProvider _clock;

    /// <summary>Creates the deterministic executor with narrow Hub gateways and the local review boundary.</summary>
    public RecoveryActivities(IEnergyReadGateway reads, IEnergyCommandGateway commands,
        ICommandCenterStageReader stages, RecoveryAuthorization authorization, TimeProvider clock)
    {
        _reads = reads;
        _commands = commands;
        _stages = stages;
        _authorization = authorization;
        _clock = clock;
    }

    /// <summary>Reads the current Hub state for validation or verification.</summary>
    [Function(nameof(ReadRecoveryState))]
    public Task<EnergyOperationalTwin> ReadRecoveryState([ActivityTrigger] RecoveryRequest request, CancellationToken cancellationToken) =>
        _reads.GetStateAsync(request.AssetId, request.CorrelationId, cancellationToken);

    /// <summary>Checks the stage and its epoch, detecting withdrawal even if the presenter later restores the stage.</summary>
    [Function(nameof(IsRecoveryStageCurrent))]
    public async Task<bool> IsRecoveryStageCurrent([ActivityTrigger] RecoveryRequest request, CancellationToken cancellationToken)
    {
        var stage = await _stages.GetCurrentStageAsync(request.CorrelationId, cancellationToken);
        return stage.Id >= DemoStage.Workflow && stage.AppliedAt == request.StageAppliedAt;
    }

    /// <summary>Revalidates the signed review and authoritative state before attempting the fixed-revision command.</summary>
    [Function(nameof(ExecuteRecovery))]
    public async Task<RecoveryResult> ExecuteRecovery([ActivityTrigger] RecoveryExecution execution, CancellationToken cancellationToken)
    {
        var request = execution.Request;
        var twin = await ReadRecoveryState(request, cancellationToken);

        // A prior attempt may have completed at the Hub before the activity result was committed.
        // Recover its receipt instead of sending another command. Never change the expected revision.
        if (twin.LastCommand?.CorrelationId == request.CorrelationId)
        {
            return ObserveReceipt(twin, request);
        }

        if (twin.StateRevision != request.ExpectedRevision)
        {
            // The Hub claims a revision before publishing its pending receipt. A redelivery can
            // land in that gap, or after a newer command replaced our receipt. Neither proves
            // that the earlier attempt did not execute. Reconcile; never send against a new revision.
            return new RecoveryResult("CommandPending", null, null,
                "The revision changed without a matching receipt; the earlier command outcome needs reconciliation.");
        }

        if (!await IsRecoveryStageCurrent(request, cancellationToken))
        {
            return new RecoveryResult("StageWithdrawn", false, !twin.IsAnomalous, "The Command Center stage changed; start a new operation.");
        }

        var policy = RemediationPolicy.Evaluate(twin);
        if (!policy.ActionRequired)
        {
            return new RecoveryResult("NothingToDo", false, true, policy.Reason);
        }

        if (execution.Review.InstanceId != execution.InstanceId
            || execution.Review.AssetId != request.AssetId
            || execution.Review.Revision != request.ExpectedRevision
            || execution.Decision is null
            || !_authorization.Verify(execution.Decision, execution.Review, _clock.GetUtcNow()))
        {
            return new RecoveryResult("ApprovalInvalid", false, false, "A current, authenticated approval for this operation is required.");
        }

        if (!execution.Decision.Approved)
        {
            return new RecoveryResult("Declined", false, false, "The supervisor declined; no command was sent.");
        }

        // At-least-once activity retries carry exactly the same expected revision. The Hub atomically
        // claims a new revision before issuing a device command, so a replay cannot issue it twice.
        var result = await _commands.RestoreScheduledModeAsync(request.AssetId, request.CorrelationId,
            request.ExpectedRevision, cancellationToken);
        twin = await ReadRecoveryState(request, cancellationToken);
        var observation = ObserveReceipt(twin, request);
        return observation.CommandExecuted == true
            ? observation with { State = "Completed" }
            : observation with { Summary = $"{result.Summary} {observation.Summary}" };
    }

    /// <summary>Reconciles an earlier command by reading its Hub receipt; this activity never sends a command.</summary>
    [Function(nameof(ReconcileRecovery))]
    public async Task<RecoveryResult> ReconcileRecovery([ActivityTrigger] RecoveryRequest request, CancellationToken cancellationToken) =>
        ObserveReceipt(await ReadRecoveryState(request, cancellationToken), request);

    private static RecoveryResult ObserveReceipt(EnergyOperationalTwin twin, RecoveryRequest request)
    {
        if (twin.LastCommand?.CorrelationId != request.CorrelationId)
        {
            return new RecoveryResult("CommandPending", null, null, "A matching command receipt is not available yet.");
        }

        return twin.LastCommand.Status switch
        {
            CommandExecutionStatus.Succeeded => new RecoveryResult("Recovered", true, !twin.IsAnomalous,
                "The matching Hub receipt confirms success; verification reread its authoritative state."),
            CommandExecutionStatus.Pending => new RecoveryResult("CommandPending", null, null,
                "The Hub accepted the command and is still awaiting confirmation. No command was resent."),
            _ => new RecoveryResult("OutcomeUnknown", null, !twin.IsAnomalous,
                "The matching receipt does not confirm success. A failure or timeout does not prove the physical command never executed.")
        };
    }
}

/// <summary>The immutable inputs to a retriable execution activity.</summary>
public sealed record RecoveryExecution(string InstanceId, RecoveryRequest Request, RecoveryReview Review, RecoveryDecision? Decision);
