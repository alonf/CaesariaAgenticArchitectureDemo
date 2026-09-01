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
/// The policy step's decision, flowing (possibly through the approval gate) to the execute step.
/// </summary>
/// <param name="Request">The originating remediation request.</param>
/// <param name="ActionRequired">Whether the reported state disagrees with the schedule.</param>
/// <param name="RequiresApproval">Whether policy demands an operator approval before executing.</param>
/// <param name="OperatorApproved">The operator's decision, stamped by the approval gate.</param>
/// <param name="Reason">A short human-readable statement of the decision.</param>
public sealed record RemediationPlan(
    RemediationRequest Request,
    bool ActionRequired,
    bool RequiresApproval,
    bool OperatorApproved,
    string Reason);

/// <summary>
/// The execute step's outcome.
/// </summary>
/// <param name="Request">The originating remediation request.</param>
/// <param name="Executed">Whether the restore command actually restored the asset.</param>
/// <param name="Summary">A short human-readable statement of what happened.</param>
public sealed record RemediationExecution(RemediationRequest Request, bool Executed, string Summary);

/// <summary>
/// The verify step's final outcome, yielded as the workflow's output.
/// </summary>
/// <param name="Executed">Whether the restore command actually restored the asset.</param>
/// <param name="InSchedule">Whether the re-read twin now matches its schedule.</param>
/// <param name="Summary">The projector-friendly outcome summary.</param>
public sealed record RemediationOutcome(bool Executed, bool InSchedule, string Summary);

/// <summary>
/// Validates the request against reality: reads the authoritative twin so every later step
/// reasons over verified state, never over the operator's assumption.
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
/// The deterministic policy: action is required when the reported state disagrees with the
/// schedule, and an operator must approve whenever a manual override would be cleared - a human
/// put it there, so a human takes it away.
/// </summary>
public sealed class EvaluatePolicyExecutor() : Executor<RemediationAssessment, RemediationPlan>("policy")
{
    /// <inheritdoc />
    public override ValueTask<RemediationPlan> HandleAsync(
        RemediationAssessment input, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var twin = input.Twin;
        var actionRequired = twin.ReportedIsOn != twin.ExpectedScheduledState;
        var requiresApproval = actionRequired && twin.ManualOverride;
        var reason = (actionRequired, requiresApproval) switch
        {
            (false, _) => $"{twin.AssetId} already matches its schedule; nothing to execute.",
            (true, true) => $"{twin.AssetId} is {(twin.ReportedIsOn ? "on" : "off")} against its schedule under a manual override; operator approval is required to clear it.",
            (true, false) => $"{twin.AssetId} is {(twin.ReportedIsOn ? "on" : "off")} against its schedule with no manual override; restoring automatically."
        };

        return ValueTask.FromResult(new RemediationPlan(input.Request, actionRequired, requiresApproval, OperatorApproved: false, reason));
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
            cancellationToken);
        var approved = await decision;
        return input with { OperatorApproved = approved };
    }
}

/// <summary>
/// Executes the restore through the Energy Hub command gateway - or explains why it did not:
/// nothing to do, or the operator declined.
/// </summary>
public sealed class ExecuteRestoreExecutor(IEnergyCommandGateway commandGateway)
    : Executor<RemediationPlan, RemediationExecution>("execute")
{
    /// <inheritdoc />
    public override async ValueTask<RemediationExecution> HandleAsync(
        RemediationPlan input, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        if (!input.ActionRequired)
        {
            return new RemediationExecution(input.Request, Executed: false, input.Reason);
        }

        if (input.RequiresApproval && !input.OperatorApproved)
        {
            return new RemediationExecution(input.Request, Executed: false,
                $"The operator declined; {input.Request.AssetId} was left unchanged.");
        }

        var result = await commandGateway.RestoreScheduledModeAsync(input.Request.AssetId, input.Request.CorrelationId, cancellationToken);
        return new RemediationExecution(
            input.Request,
            Executed: result.Status == CommandExecutionStatus.Succeeded,
            $"{result.Status}: {result.Summary}");
    }
}

/// <summary>
/// Closes the loop: re-reads the authoritative twin and returns the workflow's final outcome -
/// the claim of success is checked against reality, never assumed from the command result. The
/// builder marks this executor with WithOutputFrom, so its result is the workflow's output.
/// </summary>
public sealed class VerifyStateExecutor(IEnergyReadGateway readGateway)
    : Executor<RemediationExecution, RemediationOutcome>("verify")
{
    /// <inheritdoc />
    public override async ValueTask<RemediationOutcome> HandleAsync(
        RemediationExecution input, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var twin = await readGateway.GetStateAsync(input.Request.AssetId, input.Request.CorrelationId, cancellationToken);
        var inSchedule = twin.ReportedIsOn == twin.ExpectedScheduledState;
        return new RemediationOutcome(
            input.Executed,
            inSchedule,
            $"{input.Summary} Verified: {twin.AssetId} is {(twin.ReportedIsOn ? "on" : "off")} and {(inSchedule ? "matches" : "still violates")} its schedule.");
    }
}
