using Microsoft.Agents.AI.Workflows;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Requests one run of the explicit remediation workflow for a single asset.
/// </summary>
/// <param name="AssetId">The streetlight asset to remediate.</param>
/// <param name="CorrelationId">The correlation identifier spanning the run.</param>
public sealed record RemediationRequest(string AssetId, string CorrelationId);

/// <summary>
/// The validate step's output: the request together with the authoritative operational twin.
/// </summary>
/// <param name="Request">The originating remediation request.</param>
/// <param name="Twin">The authoritative Energy Hub twin at validation time.</param>
public sealed record RemediationAssessment(RemediationRequest Request, EnergyOperationalTwin Twin);

/// <summary>
/// The deterministic policy decision for one observed state.
/// </summary>
/// <param name="ActionRequired">Whether the reported state disagrees with the effective target.</param>
/// <param name="RequiresApproval">Whether an operator must approve before the correction runs.</param>
/// <param name="Reason">A short human-readable statement of the decision.</param>
public sealed record RemediationPolicyDecision(bool ActionRequired, bool RequiresApproval, string Reason);

/// <summary>
/// The demo's remediation policy, written once and used by both the policy node and the
/// re-validation the execute node performs when state moved while the workflow was deciding.
/// Only rules explicitly defined for this demo appear here.
/// </summary>
public static class RemediationPolicy
{
    /// <summary>
    /// Evaluates the policy against an authoritative twin.
    /// </summary>
    /// <param name="twin">The authoritative operational twin.</param>
    /// <returns>The policy decision.</returns>
    public static RemediationPolicyDecision Evaluate(EnergyOperationalTwin twin)
    {
        ArgumentNullException.ThrowIfNull(twin);

        // The effective target already accounts for cross-domain context, so a lamp deliberately
        // lit for a security operation is not an anomaly - and is never "corrected".
        var actionRequired = twin.IsAnomalous;
        var requiresApproval = actionRequired && twin.ManualOverride;
        var state = twin.ReportedIsOn ? "on" : "off";
        var reason = (actionRequired, requiresApproval) switch
        {
            (false, _) when twin.OperationContext.RequiresLighting =>
                $"{twin.AssetId} is {state} as the active operational context requires; nothing to correct.",
            (false, _) => $"{twin.AssetId} already matches its schedule; nothing to execute.",
            (true, true) => $"{twin.AssetId} is {state} against its effective target under a manual override; operator approval is required to clear it.",
            (true, false) => $"{twin.AssetId} is {state} against its effective target with no manual override; restoring automatically."
        };

        return new RemediationPolicyDecision(actionRequired, requiresApproval, reason);
    }
}

/// <summary>
/// The policy step's decision, flowing (possibly through the approval gate) to the execute step.
/// </summary>
/// <param name="Request">The originating remediation request.</param>
/// <param name="ValidatedStateRevision">The authoritative state revision this decision was made against.</param>
/// <param name="ActionRequired">Whether the reported state disagrees with the effective target.</param>
/// <param name="RequiresApproval">Whether policy demands an operator approval before executing.</param>
/// <param name="OperatorApproved">The operator's decision, stamped by the approval gate.</param>
/// <param name="Reason">A short human-readable statement of the decision.</param>
public sealed record RemediationPlan(
    RemediationRequest Request,
    long ValidatedStateRevision,
    bool ActionRequired,
    bool RequiresApproval,
    bool OperatorApproved,
    string Reason);

/// <summary>
/// How the execute step concluded - the distinction the run view colors by.
/// </summary>
public enum RemediationExecutionResult
{
    /// <summary>The restore command executed and the Energy Hub confirmed it.</summary>
    Restored,

    /// <summary>The asset already matched its effective target; no command was issued.</summary>
    NothingToDo,

    /// <summary>The operator declined; no command was issued.</summary>
    Declined,

    /// <summary>The restore command was attempted and the Energy Hub reported failure.</summary>
    CommandFailed,

    /// <summary>State moved while the workflow was deciding, so the validated decision was refused.</summary>
    StateChanged,

    /// <summary>The demo stage left Workflow before the command was issued.</summary>
    StageWithdrawn
}

/// <summary>
/// The execute step's outcome.
/// </summary>
/// <param name="Request">The originating remediation request.</param>
/// <param name="Result">How the step concluded.</param>
/// <param name="Summary">A short human-readable statement of what happened.</param>
public sealed record RemediationExecution(RemediationRequest Request, RemediationExecutionResult Result, string Summary)
{
    /// <summary>
    /// Gets a value indicating whether a command actually changed the asset.
    /// </summary>
    public bool CommandExecuted => Result == RemediationExecutionResult.Restored;

    /// <summary>
    /// Gets a value indicating whether a command was sent to the Energy Hub at all - true even
    /// when it failed, because a failed attempt is still an attempt that must be reported.
    /// </summary>
    public bool CommandAttempted => Result is RemediationExecutionResult.Restored or RemediationExecutionResult.CommandFailed;
}

/// <summary>
/// The verify step's finding: what the workflow did, and what the authoritative state says now.
/// </summary>
/// <param name="Request">The originating remediation request.</param>
/// <param name="ExecutionResult">How the execute step concluded.</param>
/// <param name="CommandExecuted">Whether a command actually changed the asset.</param>
/// <param name="Resolved">Whether the re-read twin now matches its effective target.</param>
/// <param name="Summary">The projector-friendly finding.</param>
/// <param name="WorkItemId">The maintenance work item raised for an unresolved correction, if any.</param>
public sealed record RemediationVerification(
    RemediationRequest Request,
    RemediationExecutionResult ExecutionResult,
    bool CommandExecuted,
    bool Resolved,
    string Summary,
    string? WorkItemId = null)
{
    /// <summary>
    /// Gets a value indicating whether the correction was attempted and did not leave the asset
    /// in its effective target state - the branch that raises a maintenance work item. An
    /// operator's refusal is a decision, not a fault, so it never raises one.
    /// </summary>
    public bool RequiresWorkItem =>
        ExecutionResult == RemediationExecutionResult.CommandFailed || (CommandExecuted && !Resolved);
}

/// <summary>
/// The workflow's final, audited outcome.
/// </summary>
/// <param name="CommandExecuted">Whether a command actually changed the asset.</param>
/// <param name="Resolved">Whether the asset ended in its effective target state.</param>
/// <param name="Status">The terminal run status: Succeeded, Unresolved, or Failed.</param>
/// <param name="Summary">The projector-friendly outcome summary.</param>
/// <param name="WorkItemId">The maintenance work item raised, if any.</param>
public sealed record RemediationOutcome(
    bool CommandExecuted,
    bool Resolved,
    string Status,
    string Summary,
    string? WorkItemId);

/// <summary>
/// Validates the request against reality: reads the authoritative twin so every later step
/// reasons over verified state, never over the agent's or the operator's cached picture.
/// </summary>
public sealed class ValidateRequestExecutor(IEnergyReadGateway readGateway)
    : Executor<RemediationRequest, RemediationAssessment>("validate")
{
    /// <inheritdoc />
    public override async ValueTask<RemediationAssessment> HandleAsync(
        RemediationRequest input, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var twin = await readGateway.GetStateAsync(input.AssetId, input.CorrelationId, cancellationToken);
        return new RemediationAssessment(input, twin);
    }
}

/// <summary>
/// Applies the deterministic policy and carries the validated state revision forward, so the
/// command the workflow eventually issues is bound to the picture the decision was made on.
/// </summary>
public sealed class EvaluatePolicyExecutor() : Executor<RemediationAssessment, RemediationPlan>("policy")
{
    /// <inheritdoc />
    public override ValueTask<RemediationPlan> HandleAsync(
        RemediationAssessment input, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var decision = RemediationPolicy.Evaluate(input.Twin);

        return ValueTask.FromResult(new RemediationPlan(
            input.Request,
            input.Twin.StateRevision,
            decision.ActionRequired,
            decision.RequiresApproval,
            OperatorApproved: false,
            decision.Reason));
    }
}

/// <summary>
/// The human gate: parks the question in the pending-approval store the Command Center already
/// polls, waits for the operator's decision, and stamps it onto the plan. Cancelling the run (or
/// a stage downgrade) withdraws the question and the execute step is never reached.
/// </summary>
public sealed class OperatorApprovalExecutor(PendingApprovalStore pendingApprovals)
    : Executor<RemediationPlan, RemediationPlan>("approval")
{
    /// <inheritdoc />
    public override async ValueTask<RemediationPlan> HandleAsync(
        RemediationPlan input, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var (_, decision) = pendingApprovals.Create(
            $"Remediation workflow: restore {input.Request.AssetId} to scheduled mode? The active manual override will be cleared.",
            input.Request.CorrelationId,
            cancellationToken,
            OperationsAgentControlPoint.WorkflowGate,
            OperationsAgentToolNames.RestoreScheduledMode,
            $"assetId: {input.Request.AssetId}, stateRevision: {input.ValidatedStateRevision}");
        var approved = await decision;
        return input with { OperatorApproved = approved };
    }
}

/// <summary>
/// Executes the restore through the Energy Hub command gateway - or explains why it did not.
/// Time passes between validation and execution (an operator may think for minutes), so the
/// command carries the validated state revision and the Energy Hub refuses it if the picture
/// moved; the step then re-validates once and never escalates its own authority.
/// </summary>
public sealed class ExecuteRestoreExecutor(
    IEnergyCommandGateway commandGateway,
    IEnergyReadGateway readGateway,
    DemoStageGate stageGate)
    : Executor<RemediationPlan, RemediationExecution>("execute")
{
    /// <inheritdoc />
    public override async ValueTask<RemediationExecution> HandleAsync(
        RemediationPlan input, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        if (!input.ActionRequired)
        {
            return new RemediationExecution(input.Request, RemediationExecutionResult.NothingToDo, input.Reason);
        }

        if (input.RequiresApproval && !input.OperatorApproved)
        {
            return new RemediationExecution(input.Request, RemediationExecutionResult.Declined,
                $"The operator declined; {input.Request.AssetId} was left unchanged.");
        }

        if (WithdrawnByStage(input) is { } withdrawn)
        {
            return withdrawn;
        }

        try
        {
            var result = await commandGateway.RestoreScheduledModeAsync(
                input.Request.AssetId, input.Request.CorrelationId, input.ValidatedStateRevision, cancellationToken);

            if (!result.PreconditionFailed)
            {
                return DescribeCommand(input, result);
            }

            // The state moved under the decision. Re-validate once against the authoritative
            // picture rather than retrying blindly.
            var twin = await readGateway.GetStateAsync(input.Request.AssetId, input.Request.CorrelationId, cancellationToken);
            var decision = RemediationPolicy.Evaluate(twin);

            if (!decision.ActionRequired)
            {
                return new RemediationExecution(input.Request, RemediationExecutionResult.StateChanged,
                    $"State changed while the workflow was deciding: {decision.Reason} No command was issued.");
            }

            // Fail closed: an approval was given for a picture that no longer exists. The twin
            // cannot tell "the same override the operator saw" from "a new override asserted
            // since", so a state that still needs approval needs a *fresh* one - reusing the old
            // answer could clear an override the operator never looked at.
            if (decision.RequiresApproval)
            {
                return new RemediationExecution(input.Request, RemediationExecutionResult.StateChanged,
                    input.OperatorApproved
                        ? $"State changed after the operator approved, and the new state still requires approval: {decision.Reason} The earlier approval does not carry over, so no command was issued. Run the operation again."
                        : $"State changed while the workflow was deciding and now requires operator approval: {decision.Reason} No command was issued.");
            }

            if (WithdrawnByStage(input) is { } withdrawnBeforeRetry)
            {
                return withdrawnBeforeRetry;
            }

            var retry = await commandGateway.RestoreScheduledModeAsync(
                input.Request.AssetId, input.Request.CorrelationId, twin.StateRevision, cancellationToken);

            return retry.PreconditionFailed
                ? new RemediationExecution(input.Request, RemediationExecutionResult.StateChanged,
                    $"State kept changing while the workflow was executing; {input.Request.AssetId} was left unchanged.")
                : DescribeCommand(input, retry);
        }
        catch (Exception exception) when (IsDownstreamFailure(exception, cancellationToken))
        {
            // A boundary that cannot be reached is a failed correction, not a crashed run: it
            // flows on as data so the verify and work-item nodes still get to do their jobs.
            return new RemediationExecution(input.Request, RemediationExecutionResult.CommandFailed,
                $"The Energy Hub could not be reached to restore {input.Request.AssetId}: {exception.Message}");
        }
    }

    /// <summary>
    /// Determines whether an exception is an expected downstream failure the workflow should carry
    /// as data. Cancellation raised by the caller's own token stays an exception, because that is
    /// the run being withdrawn rather than the city failing to answer.
    /// </summary>
    /// <param name="exception">The exception to classify.</param>
    /// <param name="cancellationToken">The run's cancellation token.</param>
    /// <returns><see langword="true"/> for an expected downstream failure.</returns>
    internal static bool IsDownstreamFailure(Exception exception, CancellationToken cancellationToken) =>
        exception switch
        {
            OperationCanceledException => !cancellationToken.IsCancellationRequested,
            HttpRequestException or InvalidOperationException or TimeoutException => true,
            _ => false
        };

    private RemediationExecution? WithdrawnByStage(RemediationPlan input) =>
        // The capability itself can be withdrawn while the run waits: a stage downgrade means the
        // write no longer exists, and a decision taken at a higher stage may not execute at a
        // lower one. Checked again before a retry, because time passes there too.
        stageGate.GetCurrent().Id < DemoStage.Workflow
            ? new RemediationExecution(input.Request, RemediationExecutionResult.StageWithdrawn,
                $"The demo stage left Workflow before the command was issued; {input.Request.AssetId} was left unchanged.")
            : null;

    private static RemediationExecution DescribeCommand(RemediationPlan input, EnergyCommandOutcome outcome) =>
        new(
            input.Request,
            outcome.Status == CommandExecutionStatus.Succeeded
                ? RemediationExecutionResult.Restored
                : RemediationExecutionResult.CommandFailed,
            $"{outcome.Status}: {outcome.Summary}");
}

/// <summary>
/// Closes the loop: re-reads the authoritative twin and reports whether the asset actually ended
/// in its effective target state. A command that reported success but left the asset wrong is
/// still unresolved.
/// </summary>
public sealed class VerifyStateExecutor(IEnergyReadGateway readGateway)
    : Executor<RemediationExecution, RemediationVerification>("verify")
{
    /// <inheritdoc />
    public override async ValueTask<RemediationVerification> HandleAsync(
        RemediationExecution input, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var twin = await readGateway.GetStateAsync(input.Request.AssetId, input.Request.CorrelationId, cancellationToken);
            var resolved = !twin.IsAnomalous;

            return new RemediationVerification(
                input.Request,
                input.Result,
                input.CommandExecuted,
                resolved,
                $"{input.Summary} Verified: {twin.AssetId} is {(twin.ReportedIsOn ? "on" : "off")} and {(resolved ? "matches" : "still violates")} its effective target.");
        }
        catch (Exception exception) when (ExecuteRestoreExecutor.IsDownstreamFailure(exception, cancellationToken))
        {
            // Unverifiable is not resolved. If a command already changed the city and we cannot
            // confirm the result, that is exactly the case a maintenance work item exists for.
            return new RemediationVerification(
                input.Request,
                input.Result,
                input.CommandExecuted,
                Resolved: false,
                $"{input.Summary} Verification failed: the authoritative state for {input.Request.AssetId} could not be read ({exception.Message}).");
        }
    }
}

/// <summary>
/// The failure branch: a correction that was attempted and did not resolve raises a maintenance
/// work item, so an unfixable asset becomes someone's job instead of a silent red badge.
/// </summary>
public sealed class CreateWorkItemExecutor(IWorkItemGateway workItems)
    : Executor<RemediationVerification, RemediationVerification>("workitem")
{
    /// <inheritdoc />
    public override async ValueTask<RemediationVerification> HandleAsync(
        RemediationVerification input, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var workItem = await workItems.CreateAsync(
            input.Request.AssetId,
            $"Automated remediation did not restore {input.Request.AssetId} to its effective target. {input.Summary}",
            input.Request.CorrelationId,
            cancellationToken);

        return input with
        {
            WorkItemId = workItem.WorkItemId,
            Summary = $"{input.Summary} Maintenance work item {workItem.WorkItemId} raised."
        };
    }
}

/// <summary>
/// Publishes the audited completion: the terminal status separates "a command ran" from "the
/// city is in the state it should be", because those are different facts and the second is the
/// one that matters.
/// </summary>
public sealed class CompleteRunExecutor(ILogger<CompleteRunExecutor> logger)
    : Executor<RemediationVerification, RemediationOutcome>("complete")
{
    /// <inheritdoc />
    public override ValueTask<RemediationOutcome> HandleAsync(
        RemediationVerification input, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var status = (input.Resolved, input.RequiresWorkItem) switch
        {
            (true, _) => "Succeeded",
            (false, true) => "Failed",
            (false, false) => "Unresolved"
        };

        RemediationWorkflowLog.RunAudited(
            logger, input.Request.AssetId, status, input.CommandExecuted, input.WorkItemId ?? "none", input.Request.CorrelationId);

        return ValueTask.FromResult(new RemediationOutcome(
            input.CommandExecuted, input.Resolved, status, input.Summary, input.WorkItemId));
    }
}
