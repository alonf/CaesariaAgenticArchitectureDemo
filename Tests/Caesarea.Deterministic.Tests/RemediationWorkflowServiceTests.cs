using OperationsAgent.Api.Services;
using OperationsAgent.Contracts;

namespace Caesarea.Deterministic.Tests;

/// <summary>
/// Drives the real remediation workflow graph (validate, policy, approval gate, execute, verify)
/// through the real workflow engine with only the Energy Hub gateways faked, pinning every
/// branch the lecture demonstrates.
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

        Assert.True(finished.Executed);
        Assert.Equal(1, world.CommandGateway.RestoreCalls);
        Assert.Equal("wf-approve-corr", world.CommandGateway.LastCorrelationId);
        Assert.Equal(["validate", "policy", "approval", "execute", "verify"], finished.Steps.Select(step => step.ExecutorId));
        Assert.All(finished.Steps, step => Assert.Equal("Completed", step.Status));
        Assert.Contains("matches its schedule", finished.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DenialLeavesTheAssetUntouched()
    {
        var world = new WorkflowWorld(CreateTwin(reportedIsOn: true, manualOverride: true));

        var report = world.Service.StartRun("L-417", "wf-deny-corr");
        var approvalId = await world.WaitForApprovalAsync();

        Assert.True(world.Approvals.TryRespond(approvalId, approved: false));
        var finished = await world.WaitForCompletionAsync(report.RunId);

        Assert.False(finished.Executed);
        Assert.Equal(0, world.CommandGateway.RestoreCalls);
        Assert.Contains("declined", finished.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("still violates", finished.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnomalyWithoutOverrideExecutesWithoutApproval()
    {
        var world = new WorkflowWorld(CreateTwin(reportedIsOn: true, manualOverride: false));

        var report = world.Service.StartRun("L-417", "wf-auto-corr");
        var finished = await world.WaitForCompletionAsync(report.RunId);

        Assert.True(finished.Executed);
        Assert.Equal(1, world.CommandGateway.RestoreCalls);
        Assert.Empty(world.Approvals.GetAll());
        Assert.Equal(["validate", "policy", "execute", "verify"], finished.Steps.Select(step => step.ExecutorId));
    }

    [Fact]
    public async Task StateAlreadyInScheduleIsANoOp()
    {
        var world = new WorkflowWorld(CreateTwin(reportedIsOn: false, manualOverride: false));

        var report = world.Service.StartRun("L-417", "wf-noop-corr");
        var finished = await world.WaitForCompletionAsync(report.RunId);

        Assert.False(finished.Executed);
        Assert.Equal(0, world.CommandGateway.RestoreCalls);
        Assert.Empty(world.Approvals.GetAll());
        Assert.Contains("already matches its schedule", finished.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void DefinitionRendersTheGraphAndTheDeclarativeYaml()
    {
        var world = new WorkflowWorld(CreateTwin(reportedIsOn: true, manualOverride: true));

        var definition = world.Service.GetDefinition();

        foreach (var node in (string[])["validate", "policy", "approval", "execute", "verify"])
        {
            Assert.Contains(node, definition.Mermaid, StringComparison.Ordinal);
        }

        // The repository YAML file is the displayed declarative form; it must load, and it must
        // name the same nodes the executing graph has.
        Assert.Contains("restore-remediation", definition.Yaml, StringComparison.Ordinal);
        Assert.Contains("approval", definition.Yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("was not found", definition.Yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownRunReturnsNull()
    {
        var world = new WorkflowWorld(CreateTwin(reportedIsOn: true, manualOverride: true));

        Assert.Null(world.Service.GetRun("nope"));
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
        OperationalContext.None);

    /// <summary>
    /// The little world one run lives in: an authoritative twin the read gateway serves, a
    /// command gateway that flips it to scheduled state, and the shared approval store.
    /// </summary>
    private sealed class WorkflowWorld
    {
        private EnergyOperationalTwin _twin;

        public WorkflowWorld(EnergyOperationalTwin initialTwin)
        {
            _twin = initialTwin;
            ReadGateway = new FakeEnergyReadGateway
            {
                State = initialTwin,
                Activity = [],
                OnGetStateAsync = (_, _, _) => Task.FromResult(_twin)
            };
            CommandGateway = new FakeEnergyCommandGateway
            {
                OnRestoreScheduledModeAsync = (assetId, correlationId, _) =>
                {
                    _twin = _twin with { ReportedIsOn = _twin.ExpectedScheduledState, ManualOverride = false };
                    return Task.FromResult(new RestoreScheduledModeResult(
                        assetId, _twin.ExpectedScheduledState, _twin.ReportedIsOn,
                        CommandExecutionStatus.Succeeded, correlationId,
                        "Restored to scheduled mode.", DateTimeOffset.UtcNow));
                }
            };
            Approvals = new PendingApprovalStore(new TestTimeProvider(), NullLogger<PendingApprovalStore>.Instance);
            Service = new RemediationWorkflowService(
                ReadGateway,
                CommandGateway,
                Approvals,
                TimeProvider.System,
                NullLogger<RemediationWorkflowService>.Instance);
        }

        public FakeEnergyReadGateway ReadGateway { get; }

        public FakeEnergyCommandGateway CommandGateway { get; }

        public PendingApprovalStore Approvals { get; }

        public RemediationWorkflowService Service { get; }

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
