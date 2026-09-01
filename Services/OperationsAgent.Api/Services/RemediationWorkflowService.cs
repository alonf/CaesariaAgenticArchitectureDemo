using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Agents.AI.Workflows;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Runs the explicit remediation workflow: a code-built, deterministic orchestration graph
/// (validate, policy, an operator-approval gate when policy demands one, execute, verify) whose
/// live steps stream to the Command Center. The same graph renders itself as a Mermaid diagram,
/// and the equivalent declarative YAML is displayed beside it - one orchestration, two
/// expressions.
/// </summary>
public sealed partial class RemediationWorkflowService
{
    private static readonly TimeSpan RunBudget = TimeSpan.FromMinutes(5);

    private readonly IEnergyReadGateway _readGateway;
    private readonly IEnergyCommandGateway _commandGateway;
    private readonly PendingApprovalStore _pendingApprovals;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RemediationWorkflowService> _logger;
    private readonly ConcurrentDictionary<string, RunState> _runs = new(StringComparer.Ordinal);
    private readonly OperationsAgentWorkflowDefinition _definition;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemediationWorkflowService"/> class. The
    /// workflow definition is rendered here, at startup, from the same builder the runs use -
    /// the diagram cannot drift from the executing graph.
    /// </summary>
    /// <param name="readGateway">The authoritative Energy Hub read gateway.</param>
    /// <param name="commandGateway">The Energy Hub command gateway used by the execute step.</param>
    /// <param name="pendingApprovals">The interactive-input bridge the approval gate parks in.</param>
    /// <param name="timeProvider">The time source for step timestamps.</param>
    /// <param name="logger">The service logger.</param>
    public RemediationWorkflowService(
        IEnergyReadGateway readGateway,
        IEnergyCommandGateway commandGateway,
        PendingApprovalStore pendingApprovals,
        TimeProvider timeProvider,
        ILogger<RemediationWorkflowService> logger)
    {
        _readGateway = readGateway ?? throw new ArgumentNullException(nameof(readGateway));
        _commandGateway = commandGateway ?? throw new ArgumentNullException(nameof(commandGateway));
        _pendingApprovals = pendingApprovals ?? throw new ArgumentNullException(nameof(pendingApprovals));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _definition = new OperationsAgentWorkflowDefinition(
            WorkflowVisualizer.ToMermaidString(CreateRemediationWorkflow()),
            RemediationWorkflowYaml.Content);
    }

    /// <summary>
    /// Gets the workflow definition: the Mermaid diagram generated from the code-built graph and
    /// the equivalent declarative YAML.
    /// </summary>
    public OperationsAgentWorkflowDefinition GetDefinition() => _definition;

    /// <summary>
    /// Starts one remediation run in the background and returns its initial report immediately;
    /// the Command Center polls <see cref="GetRun"/> for live steps and the outcome.
    /// </summary>
    /// <param name="assetId">The streetlight asset to remediate.</param>
    /// <param name="correlationId">The correlation identifier spanning the run.</param>
    /// <returns>The initial run report carrying the run identifier.</returns>
    public OperationsAgentWorkflowRunReport StartRun(string assetId, string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        var runId = Guid.NewGuid().ToString("N");
        var state = new RunState(runId);
        _runs[runId] = state;
        RemediationWorkflowLog.RunStarted(_logger, runId, assetId, correlationId);

        _ = Task.Run(() => RunCoreAsync(state, new RemediationRequest(assetId, correlationId)));

        return state.ToReport();
    }

    /// <summary>
    /// Gets the live report for one run.
    /// </summary>
    /// <param name="runId">The run identifier returned by <see cref="StartRun"/>.</param>
    /// <returns>The report, or <see langword="null"/> for an unknown run.</returns>
    public OperationsAgentWorkflowRunReport? GetRun(string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        return _runs.TryGetValue(runId, out var state) ? state.ToReport() : null;
    }

    private async Task RunCoreAsync(RunState state, RemediationRequest request)
    {
        // The budget bounds an abandoned approval wait; with a debugger attached the presenter
        // may be single-stepping the graph, so the budget is suspended.
        using var timeoutSource = new CancellationTokenSource();
        timeoutSource.CancelAfter(Debugger.IsAttached ? Timeout.InfiniteTimeSpan : RunBudget);

        try
        {
            #region WORKFLOW
            DemoBreakpoints.Pause(DemoSnippets.Workflow);

            // The remediation graph, exactly as the deck draws it: validate -> policy, then
            // either straight to execute or through the operator-approval gate, and always
            // verify. Deterministic orchestration with visible branching - not emergent model
            // behavior.
            var workflow = CreateRemediationWorkflow();

            await using var run = await InProcessExecution.RunStreamingAsync(
                workflow, request, state.RunId, timeoutSource.Token);

            await foreach (var workflowEvent in run.WatchStreamAsync(timeoutSource.Token))
            {
                switch (workflowEvent)
                {
                    case ExecutorInvokedEvent invoked:
                        state.AddStep(new OperationsAgentWorkflowStep(invoked.ExecutorId, "Running", _timeProvider.GetUtcNow(), null));
                        break;
                    case ExecutorCompletedEvent completed:
                        state.AddStep(new OperationsAgentWorkflowStep(
                            completed.ExecutorId,
                            ResolveStepStatus(completed.ExecutorId, completed.Data),
                            _timeProvider.GetUtcNow(),
                            DescribeStepData(completed.ExecutorId, completed.Data)));
                        break;
                    case ExecutorFailedEvent failed:
                        state.AddStep(new OperationsAgentWorkflowStep(failed.ExecutorId, "Failed", _timeProvider.GetUtcNow(), failed.Data?.Message));
                        break;
                    case WorkflowOutputEvent output when output.Is<RemediationOutcome>():
                        var outcome = output.As<RemediationOutcome>()!;
                        state.Complete(outcome.Executed, outcome.Summary);
                        break;
                }
            }
            #endregion

            state.CompleteIfRunning("The workflow ended without yielding an outcome.");
            RemediationWorkflowLog.RunCompleted(_logger, state.RunId, state.Executed);
        }
        catch (OperationCanceledException)
        {
            state.Fail("The workflow run was cancelled - the approval wait timed out or the demo stage moved back.");
            RemediationWorkflowLog.RunCancelled(_logger, state.RunId);
        }
        catch (Exception exception)
        {
            state.Fail($"The workflow run failed: {exception.Message}");
            RemediationWorkflowLog.RunFailed(_logger, state.RunId, exception);
        }
    }

    private Workflow CreateRemediationWorkflow()
    {
        var validate = new ValidateRequestExecutor(_readGateway);
        var policy = new EvaluatePolicyExecutor();
        var approval = new OperatorApprovalExecutor(_pendingApprovals);
        var execute = new ExecuteRestoreExecutor(_commandGateway);
        var verify = new VerifyStateExecutor(_readGateway);

        return new WorkflowBuilder(validate)
            .WithName("restore-remediation")
            .AddEdge(validate, policy)
            .AddEdge<RemediationPlan>(policy, approval, plan => plan is { RequiresApproval: true })
            .AddEdge<RemediationPlan>(policy, execute, plan => plan is not { RequiresApproval: true })
            .AddEdge(approval, execute)
            .AddEdge(execute, verify)
            .WithOutputFrom(verify)
            .Build();
    }

    // A node that ran is not automatically a node that helped: the status carries the semantic
    // outcome, so a denied gate, a skipped restore, or a still-violating verification never
    // paints green in the run view.
    private static string ResolveStepStatus(string executorId, object? data) => data switch
    {
        RemediationPlan plan when executorId == "approval" => plan.OperatorApproved ? "Completed" : "Declined",
        RemediationExecution execution => execution.Result switch
        {
            RemediationExecutionResult.Restored => "Completed",
            RemediationExecutionResult.CommandFailed => "Failed",
            _ => "Skipped"
        },
        RemediationOutcome outcome => outcome.InSchedule ? "Completed" : "Unresolved",
        _ => "Completed"
    };

    private static string? DescribeStepData(string executorId, object? data) => data switch
    {
        RemediationAssessment assessment =>
            $"{assessment.Twin.AssetId} is {(assessment.Twin.ReportedIsOn ? "on" : "off")}, schedule expects {(assessment.Twin.ExpectedScheduledState ? "on" : "off")}, manual override {(assessment.Twin.ManualOverride ? "active" : "clear")}.",
        RemediationPlan plan when executorId == "approval" =>
            plan.OperatorApproved ? "The operator approved." : "The operator declined.",
        RemediationPlan plan => plan.Reason,
        RemediationExecution execution => execution.Summary,
        RemediationOutcome outcome => outcome.Summary,
        _ => null
    };

    private sealed class RunState(string runId)
    {
        private readonly Lock _gate = new();
        private readonly List<OperationsAgentWorkflowStep> _steps = [];
        private bool _finished;

        public string RunId { get; } = runId;

        public bool Executed { get; private set; }

        private string _summary = "The workflow is running.";

        public void AddStep(OperationsAgentWorkflowStep step)
        {
            lock (_gate)
            {
                // A step's completion replaces its "Running" entry, keeping one row per node.
                var index = _steps.FindLastIndex(existing => existing.ExecutorId == step.ExecutorId && existing.Status == "Running");
                if (index >= 0 && step.Status != "Running")
                {
                    _steps[index] = step;
                }
                else
                {
                    _steps.Add(step);
                }
            }
        }

        public void Complete(bool executed, string summary)
        {
            lock (_gate)
            {
                _finished = true;
                Executed = executed;
                _summary = summary;
            }
        }

        public void CompleteIfRunning(string summary)
        {
            lock (_gate)
            {
                if (!_finished)
                {
                    _finished = true;
                    _summary = summary;
                }
            }
        }

        public void Fail(string summary)
        {
            lock (_gate)
            {
                if (!_finished)
                {
                    _finished = true;
                    Executed = false;
                    _summary = summary;
                }
            }
        }

        public OperationsAgentWorkflowRunReport ToReport()
        {
            lock (_gate)
            {
                return new OperationsAgentWorkflowRunReport(
                    RunId,
                    _finished,
                    Executed,
                    _finished ? _summary : "The workflow is running.",
                    [.. _steps]);
            }
        }
    }
}

internal static partial class RemediationWorkflowLog
{
    [LoggerMessage(
        EventId = 2640,
        Level = LogLevel.Information,
        Message = "Remediation workflow run {RunId} started for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void RunStarted(ILogger logger, string runId, string assetId, string correlationId);

    [LoggerMessage(
        EventId = 2641,
        Level = LogLevel.Information,
        Message = "Remediation workflow run {RunId} completed. Executed: {Executed}.")]
    internal static partial void RunCompleted(ILogger logger, string runId, bool executed);

    [LoggerMessage(
        EventId = 2642,
        Level = LogLevel.Warning,
        Message = "Remediation workflow run {RunId} was cancelled.")]
    internal static partial void RunCancelled(ILogger logger, string runId);

    [LoggerMessage(
        EventId = 2643,
        Level = LogLevel.Error,
        Message = "Remediation workflow run {RunId} failed.")]
    internal static partial void RunFailed(ILogger logger, string runId, Exception exception);
}
