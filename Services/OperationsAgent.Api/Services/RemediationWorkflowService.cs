using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Agents.AI.Workflows;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Runs the explicit remediation workflow: a code-built, deterministic orchestration graph
/// (validate, policy, an operator-approval gate when policy demands one, execute, verify, a
/// maintenance work item when the correction did not hold, complete) whose live steps stream to
/// the Command Center. The same graph renders itself as a Mermaid diagram, and the equivalent
/// declarative YAML is displayed beside it - one orchestration, two expressions.
/// </summary>
public sealed partial class RemediationWorkflowService
{
    private static readonly TimeSpan RunBudget = TimeSpan.FromMinutes(5);
    private const int MaxRetainedRuns = 20;

    private readonly IEnergyReadGateway _readGateway;
    private readonly IEnergyCommandGateway _commandGateway;
    private readonly IWorkItemGateway _workItems;
    private readonly PendingApprovalStore _pendingApprovals;
    private readonly DemoStageGate _stageGate;
    private readonly TimeProvider _timeProvider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<RemediationWorkflowService> _logger;
    private readonly ConcurrentDictionary<string, RunState> _runs = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> _runOrder = new();
    private readonly Lock _startGate = new();
    private readonly OperationsAgentWorkflowDefinition _definition;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemediationWorkflowService"/> class. The
    /// workflow definition is rendered here, at startup, from the same builder the runs use -
    /// the diagram cannot drift from the executing graph.
    /// </summary>
    /// <param name="readGateway">The authoritative Energy Hub read gateway.</param>
    /// <param name="commandGateway">The Energy Hub command gateway used by the execute step.</param>
    /// <param name="workItems">The work-management emulator used by the failure branch.</param>
    /// <param name="pendingApprovals">The interactive-input bridge the approval gate parks in.</param>
    /// <param name="stageGate">The demo stage gate rechecked immediately before any command.</param>
    /// <param name="timeProvider">The time source for step timestamps.</param>
    /// <param name="loggerFactory">The factory used for executor loggers.</param>
    /// <param name="logger">The service logger.</param>
    public RemediationWorkflowService(
        IEnergyReadGateway readGateway,
        IEnergyCommandGateway commandGateway,
        IWorkItemGateway workItems,
        PendingApprovalStore pendingApprovals,
        DemoStageGate stageGate,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        ILogger<RemediationWorkflowService> logger)
    {
        _readGateway = readGateway ?? throw new ArgumentNullException(nameof(readGateway));
        _commandGateway = commandGateway ?? throw new ArgumentNullException(nameof(commandGateway));
        _workItems = workItems ?? throw new ArgumentNullException(nameof(workItems));
        _pendingApprovals = pendingApprovals ?? throw new ArgumentNullException(nameof(pendingApprovals));
        _stageGate = stageGate ?? throw new ArgumentNullException(nameof(stageGate));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
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
    public OperationsAgentWorkflowRunReport StartRun(string assetId, string correlationId) =>
        TryStartRun(assetId, correlationId, out var report)
            ? report
            : throw new InvalidOperationException($"A remediation workflow run is already in progress for {assetId}.");

    /// <summary>
    /// Starts one remediation run unless the asset already has one in flight. Two concurrent
    /// corrections of the same asset would race each other's preconditions and confuse the
    /// operator about which approval belongs to which run, so one asset gets one run.
    /// </summary>
    /// <param name="assetId">The streetlight asset to remediate.</param>
    /// <param name="correlationId">The correlation identifier spanning the run.</param>
    /// <param name="report">The initial run report when a run was started.</param>
    /// <returns><see langword="false"/> when a run for this asset is already active.</returns>
    public bool TryStartRun(string assetId, string correlationId, out OperationsAgentWorkflowRunReport report)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        lock (_startGate)
        {
            if (_runs.Values.Any(existing => !existing.IsFinished
                && string.Equals(existing.AssetId, assetId, StringComparison.OrdinalIgnoreCase)))
            {
                report = null!;
                return false;
            }

            report = StartRunCore(assetId, correlationId);
            return true;
        }
    }

    private OperationsAgentWorkflowRunReport StartRunCore(string assetId, string correlationId)
    {
        var runId = Guid.NewGuid().ToString("N");
        var state = new RunState(runId, assetId);
        _runs[runId] = state;
        _runOrder.Enqueue(runId);
        PruneRuns();
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

    /// <summary>
    /// Cancels every run still in flight - used when the demo stage moves below Workflow, so a
    /// run composed at a higher stage cannot go on executing at a lower one.
    /// </summary>
    /// <returns>The number of runs cancelled.</returns>
    public int CancelActiveRuns()
    {
        var cancelled = _runs.Values.Count(state => state.Cancel());

        if (cancelled > 0)
        {
            RemediationWorkflowLog.RunsCancelled(_logger, cancelled);
        }

        return cancelled;
    }

    private void PruneRuns()
    {
        // A presenter console needs the recent tail, not every run since startup.
        while (_runs.Count > MaxRetainedRuns && _runOrder.TryDequeue(out var oldestRunId))
        {
            if (_runs.TryGetValue(oldestRunId, out var oldest) && !oldest.IsFinished)
            {
                // Never evict a run that is still executing; retire it after it finishes.
                _runOrder.Enqueue(oldestRunId);
                return;
            }

            _runs.TryRemove(oldestRunId, out _);
        }
    }

    private async Task RunCoreAsync(RunState state, RemediationRequest request)
    {
        // The budget bounds an abandoned approval wait; with a debugger attached the presenter
        // may be single-stepping the graph, so the budget is suspended.
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(state.CancellationToken);
        timeoutSource.CancelAfter(Debugger.IsAttached ? Timeout.InfiniteTimeSpan : RunBudget);

        try
        {
            #region WORKFLOW
            DemoBreakpoints.Pause(DemoSnippets.Workflow);

            // The remediation graph, exactly as the deck draws it: validate, decide by policy,
            // gate on a human when an override would be cleared, execute, verify against reality,
            // raise a maintenance work item when the correction did not hold, and complete.
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
                        state.Complete(outcome);
                        break;
                }
            }
            #endregion

            // A cancelled approval surfaces as a failed executor rather than a thrown stream, so
            // the terminal status is decided by whether the run itself was withdrawn.
            if (state.CancellationRequested)
            {
                state.CompleteIfRunning("Cancelled", "The workflow run was cancelled - the demo stage moved back before it finished.");
                RemediationWorkflowLog.RunCancelled(_logger, state.RunId);
                return;
            }

            state.CompleteIfRunning("Unresolved", "The workflow ended without yielding an outcome.");
            RemediationWorkflowLog.RunCompleted(_logger, state.RunId, state.Status);
        }
        catch (OperationCanceledException)
        {
            state.Fail("Cancelled", "The workflow run was cancelled - the approval wait timed out, or the demo stage moved back.");
            RemediationWorkflowLog.RunCancelled(_logger, state.RunId);
        }
        catch (Exception exception)
        {
            state.Fail("Failed", $"The workflow run failed: {exception.Message}");
            RemediationWorkflowLog.RunFailed(_logger, state.RunId, exception);
        }
    }

    /// <summary>
    /// Builds the remediation graph for inspection. Tests compare the displayed declarative YAML
    /// against this - the very graph that runs - so the two expressions cannot drift.
    /// </summary>
    /// <returns>The workflow.</returns>
    internal Workflow BuildWorkflowForInspection() => CreateRemediationWorkflow();

    private Workflow CreateRemediationWorkflow()
    {
        var validate = new ValidateRequestExecutor(_readGateway);
        var policy = new EvaluatePolicyExecutor();
        var approval = new OperatorApprovalExecutor(_pendingApprovals);
        var execute = new ExecuteRestoreExecutor(_commandGateway, _readGateway, _stageGate);
        var verify = new VerifyStateExecutor(_readGateway);
        var workItem = new CreateWorkItemExecutor(_workItems);
        var complete = new CompleteRunExecutor(_loggerFactory.CreateLogger<CompleteRunExecutor>());

        return new WorkflowBuilder(validate)
            .WithName("restore-remediation")
            .AddEdge(validate, policy)
            .AddEdge<RemediationPlan>(policy, approval, plan => plan is { RequiresApproval: true })
            .AddEdge<RemediationPlan>(policy, execute, plan => plan is not { RequiresApproval: true })
            .AddEdge(approval, execute)
            .AddEdge(execute, verify)
            .AddEdge<RemediationVerification>(verify, workItem, found => found is { RequiresWorkItem: true })
            .AddEdge<RemediationVerification>(verify, complete, found => found is not { RequiresWorkItem: true })
            .AddEdge(workItem, complete)
            .WithOutputFrom(complete)
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
        RemediationVerification verification when executorId == "verify" => verification.Resolved ? "Completed" : "Unresolved",
        RemediationVerification => "Completed",
        RemediationOutcome outcome => outcome.Status switch
        {
            "Succeeded" => "Completed",
            "Failed" => "Failed",
            _ => "Unresolved"
        },
        _ => "Completed"
    };

    private static string? DescribeStepData(string executorId, object? data) => data switch
    {
        RemediationAssessment assessment =>
            $"{assessment.Twin.AssetId} is {(assessment.Twin.ReportedIsOn ? "on" : "off")}, effective target {(assessment.Twin.EffectiveTargetIsOn ? "on" : "off")}, manual override {(assessment.Twin.ManualOverride ? "active" : "clear")}.",
        RemediationPlan plan when executorId == "approval" =>
            plan.OperatorApproved ? "The operator approved." : "The operator declined.",
        RemediationPlan plan => plan.Reason,
        RemediationExecution execution => execution.Summary,
        RemediationVerification verification when executorId == "workitem" =>
            $"Maintenance work item {verification.WorkItemId} raised.",
        RemediationVerification verification => verification.Summary,
        RemediationOutcome outcome => outcome.Summary,
        _ => null
    };

    private sealed class RunState(string runId, string assetId) : IDisposable
    {
        public string AssetId { get; } = assetId;

        private readonly Lock _gate = new();
        private readonly List<OperationsAgentWorkflowStep> _steps = [];
        private readonly CancellationTokenSource _cancellation = new();
        private bool _finished;
        private bool _commandExecuted;
        private bool _resolved;
        private string _status = "Running";
        private string _summary = "The workflow is running.";
        private string? _workItemId;

        public string RunId { get; } = runId;

        public CancellationToken CancellationToken => _cancellation.Token;

        public bool CancellationRequested => _cancellation.IsCancellationRequested;

        public bool IsFinished
        {
            get
            {
                lock (_gate)
                {
                    return _finished;
                }
            }
        }

        public string Status
        {
            get
            {
                lock (_gate)
                {
                    return _status;
                }
            }
        }

        public bool Cancel()
        {
            if (IsFinished || _cancellation.IsCancellationRequested)
            {
                return false;
            }

            _cancellation.Cancel();
            return true;
        }

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

                // A command that ran is a fact the run keeps even if the run later fails.
                if (step.ExecutorId == "execute" && step.Status == "Completed")
                {
                    _commandExecuted = true;
                }
            }
        }

        public void Complete(RemediationOutcome outcome)
        {
            lock (_gate)
            {
                _finished = true;
                _commandExecuted |= outcome.CommandExecuted;
                _resolved = outcome.Resolved;
                _status = outcome.Status;
                _summary = outcome.Summary;
                _workItemId = outcome.WorkItemId;
            }
        }

        public void CompleteIfRunning(string status, string summary)
        {
            lock (_gate)
            {
                if (!_finished)
                {
                    _finished = true;
                    _status = status;
                    _summary = summary;
                }
            }
        }

        public void Fail(string status, string summary)
        {
            lock (_gate)
            {
                if (!_finished)
                {
                    // _commandExecuted is deliberately preserved: a run that failed after the
                    // command landed must not report that nothing happened.
                    _finished = true;
                    _resolved = false;
                    _status = status;
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
                    _commandExecuted,
                    _resolved,
                    _status,
                    _summary,
                    _workItemId,
                    [.. _steps]);
            }
        }

        public void Dispose() => _cancellation.Dispose();
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
        Message = "Remediation workflow run {RunId} completed with status {Status}.")]
    internal static partial void RunCompleted(ILogger logger, string runId, string status);

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

    [LoggerMessage(
        EventId = 2644,
        Level = LogLevel.Warning,
        Message = "{CancelledCount} in-flight remediation workflow run(s) cancelled by a stage downgrade.")]
    internal static partial void RunsCancelled(ILogger logger, int cancelledCount);

    [LoggerMessage(
        EventId = 2645,
        Level = LogLevel.Information,
        Message = "Remediation workflow audit for asset {AssetId}: status {Status}, command executed {CommandExecuted}, work item {WorkItemId}. CorrelationId: {CorrelationId}.")]
    internal static partial void RunAudited(ILogger logger, string assetId, string status, bool commandExecuted, string workItemId, string correlationId);
}
