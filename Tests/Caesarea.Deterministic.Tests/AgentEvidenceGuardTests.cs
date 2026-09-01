using CommandCenter.Web.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class AgentEvidenceGuardTests
{
    private const string AgentCorrelation = "agent-run-corr";

    [Fact]
    public void UnchangedStateKeepsTheAnswer()
    {
        var snapshot = CreateSnapshot();
        var requested = AgentEvidenceGuard.GetEvidenceVersion(snapshot);

        var current = AgentEvidenceGuard.IsAnswerCurrent(requested, snapshot, AgentCorrelation, out var version);

        Assert.True(current);
        Assert.Equal(requested, version);
    }

    [Fact]
    public void OwnApprovedWriteKeepsTheAnswerAndAdvancesTheVersion()
    {
        // The High-severity regression this guard prevents: the agent's own approved restore
        // changes the last command and telemetry timestamp - that must not invalidate the very
        // answer that reported the restore.
        var before = CreateSnapshot();
        var requested = AgentEvidenceGuard.GetEvidenceVersion(before);
        var after = WithCommand(before, AgentCorrelation) with
        {
            OperationalState = WithCommand(before, AgentCorrelation).OperationalState with
            {
                LastReportedAt = before.OperationalState.LastReportedAt.AddSeconds(4)
            }
        };

        var current = AgentEvidenceGuard.IsAnswerCurrent(requested, after, AgentCorrelation, out var version);

        Assert.True(current);
        Assert.Equal(AgentEvidenceGuard.GetEvidenceVersion(after), version);
        Assert.NotEqual(requested, version);
    }

    [Fact]
    public void ForeignCommandInvalidatesTheAnswer()
    {
        var before = CreateSnapshot();
        var requested = AgentEvidenceGuard.GetEvidenceVersion(before);
        var after = WithCommand(before, "someone-elses-corr");

        Assert.False(AgentEvidenceGuard.IsAnswerCurrent(requested, after, AgentCorrelation, out _));
    }

    [Fact]
    public void AmbientChangeInvalidatesTheAnswerEvenWithOwnCommand()
    {
        // The own-write exemption covers exactly the write: if anything else changed too (here a
        // new customer report), the answer no longer describes the state on screen.
        var before = CreateSnapshot();
        var requested = AgentEvidenceGuard.GetEvidenceVersion(before);
        var after = WithCommand(before, AgentCorrelation) with
        {
            CustomerReport = new CustomerReportRecord(
                "RPT-9", "L-417", "Harbor", "Still glowing!", "img", "App", DateTimeOffset.UtcNow, "new-report-corr")
        };

        Assert.False(AgentEvidenceGuard.IsAnswerCurrent(requested, after, AgentCorrelation, out _));
    }

    [Fact]
    public void MissingCorrelationFallsBackToStrictComparison()
    {
        var before = CreateSnapshot();
        var requested = AgentEvidenceGuard.GetEvidenceVersion(before);
        var after = WithCommand(before, AgentCorrelation);

        Assert.False(AgentEvidenceGuard.IsAnswerCurrent(requested, after, answerCorrelationId: null, out _));
        Assert.False(AgentEvidenceGuard.IsAnswerCurrent(requested, after, answerCorrelationId: string.Empty, out _));
    }

    [Fact]
    public void MissingSnapshotYieldsNullVersion()
    {
        Assert.Null(AgentEvidenceGuard.GetEvidenceVersion(null));
        Assert.True(AgentEvidenceGuard.IsAnswerCurrent(null, null, AgentCorrelation, out var version));
        Assert.Null(version);
    }

    private static CommandCenterSnapshot CreateSnapshot()
    {
        var now = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
        return new CommandCenterSnapshot(
            "L-417",
            "Harbor Promenade",
            "Streetlight L-417",
            new EnergyOperationalTwin(
                "L-417",
                "Harbor Promenade",
                ReportedIsOn: true,
                DesiredIsOn: true,
                IsDaylight: true,
                ExpectedScheduledState: false,
                ManualOverride: true,
                ControllerHealthInfo.Healthy,
                LastCommand: null,
                LastMaintenanceTime: null,
                HasRecentMaintenance: false,
                OpenIncidentId: null,
                LastReportedAt: now,
                OperationalContext.None),
            new ScenarioStatus(ScenarioId.ForgottenOverride, "Forgotten override", "Test scenario", now, "scenario-corr"),
            CustomerReport: null,
            OpenIncident: null,
            RecentActivity: [],
            new DemoStageStatus(DemoStage.InteractiveInput, "InteractiveInput", "Test stage", ["Test"], now, "stage-corr"));
    }

    private static CommandCenterSnapshot WithCommand(CommandCenterSnapshot snapshot, string correlationId) =>
        snapshot with
        {
            OperationalState = snapshot.OperationalState with
            {
                LastCommand = new CommandRecord(
                    "RestoreScheduledMode",
                    DesiredIsOn: false,
                    CommandExecutionStatus.Succeeded,
                    correlationId,
                    snapshot.OperationalState.LastReportedAt,
                    snapshot.OperationalState.LastReportedAt.AddSeconds(2),
                    "Restored to scheduled mode.")
            }
        };
}
