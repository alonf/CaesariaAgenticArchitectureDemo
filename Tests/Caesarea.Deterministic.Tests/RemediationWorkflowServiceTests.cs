using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;
using OperationsAgent.Api.Services;
using OperationsAgent.Contracts;

namespace Caesarea.Deterministic.Tests;

/// <summary>
/// Drives the real remediation workflow graph through the real workflow engine with only the
/// Energy Hub gateways faked, pinning every branch the lecture demonstrates - including the ones
/// that must refuse to act.
/// </summary>
public sealed class RemediationWorkflowServiceTests
{
    [Fact]
    public async Task OverrideAnomalyPausesForApprovalAndExecutesOnApprove()
    {
        var world = new WorkflowWorld(CreateTwin(reportedIsOn: true, manualOverride: true));

        var report = world.Service.StartRun("L-417", "wf-approve-corr");
        var approvalId = await world.WaitForApprovalAsync();

        Assert.True(world.Approvals.TryRespond(approvalId, approved: true));
        var finished = await world.WaitForCompletionAsync(report.RunId);

        Assert.True(finished.CommandExecuted);
        Assert.True(finished.Resolved);
        Assert.Equal("Succeeded", finished.Status);
        Assert.Null(finished.WorkItemId);
        Assert.Equal(1, world.CommandGateway.RestoreCalls);
        Assert.Equal("wf-approve-corr", world.CommandGateway.LastCorrelationId);
        Assert.Equal(
            ["validate", "policy", "approval", "execute", "verify", "complete"],
            finished.Steps.Select(step => step.ExecutorId));
        Assert.All(finished.Steps, step => Assert.Equal("Completed", step.Status));
    }

    [Fact]
    public async Task DenialLeavesTheAssetUntouchedAndRaisesNoWorkItem()
    {
        var world = new WorkflowWorld(CreateTwin(reportedIsOn: true, manualOverride: true));

        var report = world.Service.StartRun("L-417", "wf-deny-corr");
        var approvalId = await world.WaitForApprovalAsync();

        Assert.True(world.Approvals.TryRespond(approvalId, approved: false));
        var finished = await world.WaitForCompletionAsync(report.RunId);

        Assert.False(finished.CommandExecuted);
        Assert.False(finished.Resolved);
        Assert.Equal("Unresolved", finished.Status);
        Assert.Equal(0, world.CommandGateway.RestoreCalls);
        // A refusal is a decision, not a fault: no maintenance work item is raised.
        Assert.Null(finished.WorkItemId);
        Assert.Empty(world.WorkItems.Created);
        Assert.Equal(
            [("approval", "Declined"), ("execute", "Skipped"), ("verify", "Unresolved")],
            finished.Steps.Where(step => step.ExecutorId is "approval" or "execute" or "verify")
                .Select(step => (step.ExecutorId, step.Status)));
    }

    [Fact]
    public async Task AnomalyWithoutOverrideExecutesWithoutApproval()
    {
        var world = new WorkflowWorld(CreateTwin(reportedIsOn: true, manualOverride: false));

        var report = world.Service.StartRun("L-417", "wf-auto-corr");
        var finished = await world.WaitForCompletionAsync(report.RunId);

        Assert.True(finished.CommandExecuted);
        Assert.True(finished.Resolved);
        Assert.Empty(world.Approvals.GetAll());
        Assert.Equal(
            ["validate", "policy", "execute", "verify", "complete"],
            finished.Steps.Select(step => step.ExecutorId));
    }

    [Fact]
    public async Task StateAlreadyInScheduleIsANoOp()
    {
        var world = new WorkflowWorld(CreateTwin(reportedIsOn: false, manualOverride: false));

        var report = world.Service.StartRun("L-417", "wf-noop-corr");
        var finished = await world.WaitForCompletionAsync(report.RunId);

        Assert.False(finished.CommandExecuted);
        Assert.True(finished.Resolved);
        Assert.Equal("Succeeded", finished.Status);
        Assert.Equal(0, world.CommandGateway.RestoreCalls);
        Assert.Equal("Skipped", finished.Steps.Single(step => step.ExecutorId == "execute").Status);
    }

    [Fact]
    public async Task RequiredSecurityLightingIsNeverRemediated()
    {
        // The lamp is deliberately on for a security operation while the daylight schedule says
        // off. Judging that against the raw schedule would switch the lights off under the
        // operation; the effective target says it is exactly where it should be.
        var world = new WorkflowWorld(CreateTwin(reportedIsOn: true, manualOverride: false) with
        {
            OperationContext = new OperationalContext(true, "An external operational directive requires lighting in this area.")
        });

        var report = world.Service.StartRun("L-417", "wf-security-corr");
        var finished = await world.WaitForCompletionAsync(report.RunId);

        Assert.Equal(0, world.CommandGateway.RestoreCalls);
        Assert.False(finished.CommandExecuted);
        Assert.True(finished.Resolved);
        Assert.Equal("Succeeded", finished.Status);
        Assert.Empty(world.Approvals.GetAll());
        Assert.Contains("operational context requires", finished.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OverrideAppearingAfterAnAutomaticDecisionIsNotClearedWithoutApproval()
    {
        // The reviewer's hazard: the automatic branch validated a state with no manual override,
        // and an operator adds one before the command lands. The stale command must be refused,
        // and the re-validated policy now demands an approval nobody gave.
        var world = new WorkflowWorld(CreateTwin(reportedIsOn: true, manualOverride: false));
        world.MutateOnNextCommand(twin => twin with { ManualOverride = true });

        var report = world.Service.StartRun("L-417", "wf-race-corr");
        var finished = await world.WaitForCompletionAsync(report.RunId);

        Assert.False(finished.CommandExecuted);
        Assert.Equal("Unresolved", finished.Status);
        Assert.True(world.Twin.ManualOverride);
        // One refused attempt, and no second command issued on the strength of the old decision.
        Assert.Equal(1, world.CommandGateway.RestoreCalls);
        Assert.Contains("requires operator approval", finished.Steps.Single(step => step.ExecutorId == "execute").Detail!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApprovalDoesNotCarryOverToAStateThatStillNeedsApproval()
    {
        // The operator approved clearing the override they were shown, and the picture moved
        // while they decided. The twin cannot tell "the same override" from "a new one asserted
        // since", so the approval must not carry over: fail closed and ask again.
        var world = new WorkflowWorld(CreateTwin(reportedIsOn: true, manualOverride: true));
        world.MutateOnNextCommand(twin => twin with { LastReportedAt = twin.LastReportedAt.AddMinutes(1) });

        var report = world.Service.StartRun("L-417", "wf-stale-approval-corr");
        var approvalId = await world.WaitForApprovalAsync();
        Assert.True(world.Approvals.TryRespond(approvalId, approved: true));
        var finished = await world.WaitForCompletionAsync(report.RunId);

        Assert.False(finished.CommandExecuted);
        Assert.Equal("Unresolved", finished.Status);
        Assert.True(world.Twin.ManualOverride);
        // Exactly one refused attempt: no command was issued on the strength of the old approval.
        Assert.Equal(1, world.CommandGateway.RestoreCalls);
        Assert.Contains("does not carry over", finished.Steps.Single(step => step.ExecutorId == "execute").Detail!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StateChangeThatNoLongerNeedsApprovalRetriesOnceAgainstTheFreshRevision()
    {
        // Harmless movement on the automatic branch: policy still says act, still without an
        // approval, so one bounded retry executes - against the new revision, never the stale one.
        var world = new WorkflowWorld(CreateTwin(reportedIsOn: true, manualOverride: false));
        world.MutateOnNextCommand(twin => twin with { LastReportedAt = twin.LastReportedAt.AddMinutes(1) });

        var report = world.Service.StartRun("L-417", "wf-retry-corr");
        var finished = await world.WaitForCompletionAsync(report.RunId);

        Assert.True(finished.CommandExecuted);
        Assert.True(finished.Resolved);
        Assert.Equal(2, world.CommandGateway.RestoreCalls);
        Assert.Equal(world.RevisionBeforeLastCommand, world.CommandGateway.LastExpectedStateRevision);
    }

    [Fact]
    public async Task UnreachableEnergyHubRaisesAWorkItemInsteadOfCrashingTheRun()
    {
        var world = new WorkflowWorld(CreateTwin(reportedIsOn: true, manualOverride: false));
        world.CommandGateway.OnRestoreScheduledModeAsync = (_, _, _, _) =>
            throw new HttpRequestException("Energy Hub is unreachable.");

        var report = world.Service.StartRun("L-417", "wf-transport-corr");
        var finished = await world.WaitForCompletionAsync(report.RunId);

        Assert.False(finished.CommandExecuted);
        Assert.Equal("Failed", finished.Status);
        Assert.NotNull(finished.WorkItemId);
        Assert.Single(world.WorkItems.Created);
    }

    [Fact]
    public async Task UnverifiableStateAfterACommandRaisesAWorkItem()
    {
        // The command landed and the verification read timed out. Unverifiable is not resolved,
        // and a change we cannot confirm is exactly what maintenance needs to look at.
        var world = new WorkflowWorld(CreateTwin(reportedIsOn: true, manualOverride: false));
        world.FailReadsAfterFirst(new TimeoutException("The Energy Hub read timed out."));

        var report = world.Service.StartRun("L-417", "wf-verify-timeout-corr");
        var finished = await world.WaitForCompletionAsync(report.RunId);

        Assert.True(finished.CommandExecuted);
        Assert.False(finished.Resolved);
        Assert.Equal("Failed", finished.Status);
        Assert.NotNull(finished.WorkItemId);
        Assert.Contains("Verification failed", finished.Steps.Single(step => step.ExecutorId == "verify").Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StageDowngradeBeforeExecutionWithdrawsTheCommand()
    {
        var world = new WorkflowWorld(CreateTwin(reportedIsOn: true, manualOverride: true));

        var report = world.Service.StartRun("L-417", "wf-stage-corr");
        var approvalId = await world.WaitForApprovalAsync();
        world.MoveStageTo(DemoStage.Knowledge);

        Assert.True(world.Approvals.TryRespond(approvalId, approved: true));
        var finished = await world.WaitForCompletionAsync(report.RunId);

        Assert.False(finished.CommandExecuted);
        Assert.Equal(0, world.CommandGateway.RestoreCalls);
        Assert.Contains("left Workflow", finished.Steps.Single(step => step.ExecutorId == "execute").Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedCommandRaisesAMaintenanceWorkItem()
    {
        var world = new WorkflowWorld(CreateTwin(reportedIsOn: true, manualOverride: false));
        world.CommandGateway.OnRestoreScheduledModeAsync = (_, _, _, _) =>
            Task.FromResult(new EnergyCommandOutcome(CommandExecutionStatus.Failed, "SmartPole did not confirm.", PreconditionFailed: false));

        var report = world.Service.StartRun("L-417", "wf-fail-corr");
        var finished = await world.WaitForCompletionAsync(report.RunId);

        Assert.False(finished.CommandExecuted);
        Assert.False(finished.Resolved);
        Assert.Equal("Failed", finished.Status);
        Assert.NotNull(finished.WorkItemId);
        var workItem = Assert.Single(world.WorkItems.Created);
        Assert.Equal("L-417", workItem.AssetId);
        Assert.Equal(
            ["validate", "policy", "execute", "verify", "workitem", "complete"],
            finished.Steps.Select(step => step.ExecutorId));
        Assert.Equal("Failed", finished.Steps.Single(step => step.ExecutorId == "execute").Status);
    }

    [Fact]
    public async Task CommandThatRunsButDoesNotResolveStillRaisesAWorkItem()
    {
        // The command reports success and the lamp stays wrong: "executed" is not "fixed", and
        // the run must say so rather than reporting a green success.
        var world = new WorkflowWorld(CreateTwin(reportedIsOn: true, manualOverride: false), applyRestoreToTwin: false);

        var report = world.Service.StartRun("L-417", "wf-ineffective-corr");
        var finished = await world.WaitForCompletionAsync(report.RunId);

        Assert.True(finished.CommandExecuted);
        Assert.False(finished.Resolved);
        Assert.Equal("Failed", finished.Status);
        Assert.NotNull(finished.WorkItemId);
        Assert.Equal("Unresolved", finished.Steps.Single(step => step.ExecutorId == "verify").Status);
    }

    [Fact]
    public async Task CancellingActiveRunsStopsAnUnansweredRun()
    {
        var world = new WorkflowWorld(CreateTwin(reportedIsOn: true, manualOverride: true));

        var report = world.Service.StartRun("L-417", "wf-cancel-corr");
        await world.WaitForApprovalAsync();

        Assert.Equal(1, world.Service.CancelActiveRuns());
        var finished = await world.WaitForCompletionAsync(report.RunId);

        Assert.Equal("Cancelled", finished.Status);
        Assert.Equal(0, world.CommandGateway.RestoreCalls);
        Assert.Equal(0, world.Service.CancelActiveRuns());
    }

    [Fact]
    public void DeclarativeYamlMatchesTheExecutingGraphTopology()
    {
        var world = new WorkflowWorld(CreateTwin(reportedIsOn: true, manualOverride: true));
        var definition = world.Service.GetDefinition();
        var workflow = world.BuildWorkflowForInspection();
        var yaml = ParseYamlTopology(definition.Yaml);

        // The displayed declarative form and the executing graph must describe the same
        // orchestration: same start, same nodes, same edges, same conditional branches.
        Assert.Equal(workflow.StartExecutorId, yaml.Start);
        Assert.Equal(
            workflow.ReflectExecutors().Keys.OrderBy(id => id, StringComparer.Ordinal),
            yaml.Executors.OrderBy(id => id, StringComparer.Ordinal));

        var codeEdges = workflow.ReflectEdges()
            .SelectMany(entry => entry.Value)
            .OfType<DirectEdgeInfo>()
            .Select(edge => (
                From: string.Join(",", edge.Connection.SourceIds),
                To: string.Join(",", edge.Connection.SinkIds),
                edge.HasCondition))
            .OrderBy(edge => edge.From + "->" + edge.To, StringComparer.Ordinal);

        Assert.Equal(codeEdges, yaml.Edges.OrderBy(edge => edge.From + "->" + edge.To, StringComparer.Ordinal));

        foreach (var node in yaml.Executors)
        {
            Assert.Contains(node, definition.Mermaid, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task OneAssetGetsOneActiveRun()
    {
        var world = new WorkflowWorld(CreateTwin(reportedIsOn: true, manualOverride: true));

        var report = world.Service.StartRun("L-417", "wf-single-corr");
        await world.WaitForApprovalAsync();

        // A second run would race the first one's precondition and leave the operator unsure
        // which approval belongs to which run.
        Assert.False(world.Service.TryStartRun("L-417", "wf-second-corr", out _));

        Assert.True(world.Approvals.TryRespond(world.Approvals.GetAll()[0].Id, approved: true));
        await world.WaitForCompletionAsync(report.RunId);

        // Once it finishes the asset is free again.
        Assert.True(world.Service.TryStartRun("L-417", "wf-third-corr", out var third));
        Assert.NotEqual(report.RunId, third.RunId);
    }

    [Fact]
    public void UnknownRunReturnsNull()
    {
        var world = new WorkflowWorld(CreateTwin(reportedIsOn: true, manualOverride: true));

        Assert.Null(world.Service.GetRun("nope"));
    }

    // A deliberately small hand parser: the file is a fixed, repository-owned shape, and taking a
    // YAML dependency for eight edges would cost more than it explains.
    private static (string Start, List<string> Executors, List<(string From, string To, bool HasCondition)> Edges) ParseYamlTopology(string yaml)
    {
        var start = string.Empty;
        List<string> executors = [];
        List<(string From, string To, bool HasCondition)> edges = [];
        var section = string.Empty;
        string? pendingFrom = null;
        string? pendingTo = null;
        var pendingHasCondition = false;

        void FlushEdge()
        {
            if (pendingFrom is not null && pendingTo is not null)
            {
                edges.Add((pendingFrom, pendingTo, pendingHasCondition));
            }

            pendingFrom = null;
            pendingTo = null;
            pendingHasCondition = false;
        }

        foreach (var rawLine in yaml.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');

            if (line.StartsWith('#') || string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!line.StartsWith(' ') && !line.StartsWith('-'))
            {
                FlushEdge();
                section = line.Split(':')[0].Trim();

                if (section == "start")
                {
                    start = line.Split(':', 2)[1].Trim();
                }

                continue;
            }

            var trimmed = line.Trim();

            if (section == "executors" && trimmed.StartsWith("- id:", StringComparison.Ordinal))
            {
                executors.Add(trimmed["- id:".Length..].Trim());
            }
            else if (section == "edges")
            {
                if (trimmed.StartsWith("- from:", StringComparison.Ordinal))
                {
                    FlushEdge();
                    pendingFrom = trimmed["- from:".Length..].Trim();
                }
                else if (trimmed.StartsWith("to:", StringComparison.Ordinal))
                {
                    pendingTo = trimmed["to:".Length..].Trim();
                }
                else if (trimmed.StartsWith("condition:", StringComparison.Ordinal))
                {
                    pendingHasCondition = true;
                }
            }
        }

        FlushEdge();
        return (start, executors, edges);
    }

    private static EnergyOperationalTwin CreateTwin(bool reportedIsOn, bool manualOverride) => new(
        "L-417",
        DemoAssets.NorthPromenadeArea,
        ReportedIsOn: reportedIsOn,
        DesiredIsOn: reportedIsOn,
        IsDaylight: true,
        ExpectedScheduledState: false,
        ManualOverride: manualOverride,
        ControllerHealthInfo.Healthy,
        LastCommand: null,
        LastMaintenanceTime: null,
        HasRecentMaintenance: false,
        OpenIncidentId: null,
        LastReportedAt: DateTimeOffset.UtcNow,
        OperationalContext.None,
        StateRevision: 1);

    /// <summary>
    /// The little world one run lives in: an authoritative twin with a state revision that every
    /// mutation bumps, a command gateway that honors the revision precondition, the shared
    /// approval store, the work-management emulator, and the demo stage gate.
    /// </summary>
    private sealed class WorkflowWorld
    {
        private readonly bool _applyRestoreToTwin;
        private Func<EnergyOperationalTwin, EnergyOperationalTwin>? _mutateOnNextCommand;
        private Exception? _readFailure;
        private int _reads;

        public WorkflowWorld(EnergyOperationalTwin initialTwin, bool applyRestoreToTwin = true)
        {
            Twin = initialTwin;
            _applyRestoreToTwin = applyRestoreToTwin;
            StageGate = new DemoStageGate(DemoStage.Workflow);
            ReadGateway = new FakeEnergyReadGateway
            {
                State = initialTwin,
                Activity = [],
                OnGetStateAsync = (_, _, _) =>
                {
                    if (_readFailure is { } failure && _reads++ > 0)
                    {
                        return Task.FromException<EnergyOperationalTwin>(failure);
                    }

                    _reads++;
                    return Task.FromResult(Twin);
                }
            };
            CommandGateway = new FakeEnergyCommandGateway
            {
                OnRestoreScheduledModeAsync = (_, _, expectedRevision, _) =>
                {
                    if (_mutateOnNextCommand is { } mutate)
                    {
                        _mutateOnNextCommand = null;
                        Mutate(mutate);
                    }

                    if (expectedRevision != Twin.StateRevision)
                    {
                        return Task.FromResult(new EnergyCommandOutcome(
                            CommandExecutionStatus.Failed, "Precondition failed.", PreconditionFailed: true));
                    }

                    RevisionBeforeLastCommand = Twin.StateRevision;

                    if (_applyRestoreToTwin)
                    {
                        Mutate(twin => twin with
                        {
                            ReportedIsOn = twin.EffectiveTargetIsOn,
                            DesiredIsOn = twin.EffectiveTargetIsOn,
                            ManualOverride = false
                        });
                    }

                    return Task.FromResult(new EnergyCommandOutcome(
                        CommandExecutionStatus.Succeeded, "Scheduled mode restored after SmartPole confirmation.", PreconditionFailed: false));
                }
            };
            Approvals = new PendingApprovalStore(new TestTimeProvider(), NullLogger<PendingApprovalStore>.Instance);
            WorkItems = new FakeWorkItemGateway();
            Service = new RemediationWorkflowService(
                ReadGateway,
                CommandGateway,
                WorkItems,
                Approvals,
                StageGate,
                TimeProvider.System,
                NullLoggerFactory.Instance,
                NullLogger<RemediationWorkflowService>.Instance);
        }

        public EnergyOperationalTwin Twin { get; private set; }

        public long RevisionBeforeLastCommand { get; private set; }

        public FakeEnergyReadGateway ReadGateway { get; }

        public FakeEnergyCommandGateway CommandGateway { get; }

        public PendingApprovalStore Approvals { get; }

        public FakeWorkItemGateway WorkItems { get; }

        public DemoStageGate StageGate { get; }

        public RemediationWorkflowService Service { get; }

        public void Mutate(Func<EnergyOperationalTwin, EnergyOperationalTwin> mutate) =>
            Twin = mutate(Twin) with { StateRevision = Twin.StateRevision + 1 };

        public void MutateOnNextCommand(Func<EnergyOperationalTwin, EnergyOperationalTwin> mutate) =>
            _mutateOnNextCommand = mutate;

        // Validation succeeds, then the boundary stops answering - the shape of a verification
        // timeout that happens after a command has already changed the city.
        public void FailReadsAfterFirst(Exception failure) => _readFailure = failure;

        public void MoveStageTo(DemoStage stage) =>
            StageGate.SetCurrent(new DemoStageStatus(stage, stage.ToString(), "Test stage", ["Test"], DateTimeOffset.UtcNow, "stage-corr"));

        public Workflow BuildWorkflowForInspection() => Service.BuildWorkflowForInspection();

        public async Task<string> WaitForApprovalAsync()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            for (var attempt = 0; attempt < 200; attempt++)
            {
                var pending = Approvals.GetAll();

                if (pending.Count > 0)
                {
                    return pending[0].Id;
                }

                await Task.Delay(50, cancellationToken);
            }

            throw new TimeoutException("The workflow never parked an approval request.");
        }

        public async Task<OperationsAgentWorkflowRunReport> WaitForCompletionAsync(string runId)
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            for (var attempt = 0; attempt < 400; attempt++)
            {
                var report = Service.GetRun(runId);

                if (report is { Completed: true })
                {
                    return report;
                }

                await Task.Delay(50, cancellationToken);
            }

            throw new TimeoutException("The workflow run did not complete in time.");
        }
    }
}
