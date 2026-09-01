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

        var current = AgentEvidenceGuard.IsAnswerCurrent(
            requested, snapshot, AgentCorrelation, ownWriteAlreadyAdopted: false, out var version, out var adopted);

        Assert.True(current);
        Assert.Equal(requested, version);
        Assert.False(adopted);
    }

    [Fact]
    public void OwnApprovedWriteIsAdoptedOnceAndAdvancesTheVersion()
    {
        // The agent's own approved restore changes the state revision; that must not invalidate
        // the very answer that reported the restore.
        var before = CreateSnapshot();
        var requested = AgentEvidenceGuard.GetEvidenceVersion(before);
        var after = WithOwnRestore(before, AgentCorrelation);

        var current = AgentEvidenceGuard.IsAnswerCurrent(
            requested, after, AgentCorrelation, ownWriteAlreadyAdopted: false, out var version, out var adopted);

        Assert.True(current);
        Assert.True(adopted);
        Assert.Equal(AgentEvidenceGuard.GetEvidenceVersion(after), version);
        Assert.NotEqual(requested, version);
    }

    [Fact]
    public void TelemetryChangingAfterAnAdoptedOwnWriteMakesTheAnswerStale()
    {
        // The regression the review asked for: the lamp comes back on by itself while the last
        // command is still the agent's successful restore. An answer saying "the lamp is off"
        // must not survive that.
        var before = CreateSnapshot();
        var requested = AgentEvidenceGuard.GetEvidenceVersion(before);
        var afterRestore = WithOwnRestore(before, AgentCorrelation);

        Assert.True(AgentEvidenceGuard.IsAnswerCurrent(
            requested, afterRestore, AgentCorrelation, ownWriteAlreadyAdopted: false, out var adoptedVersion, out var adopted));
        Assert.True(adopted);

        var lampBackOn = afterRestore with
        {
            OperationalState = afterRestore.OperationalState with
            {
                ReportedIsOn = true,
                StateRevision = afterRestore.OperationalState.StateRevision + 1
            }
        };

        Assert.False(AgentEvidenceGuard.IsAnswerCurrent(
            adoptedVersion, lampBackOn, AgentCorrelation, ownWriteAlreadyAdopted: true, out _, out _));
        // Even without the one-time flag the observed state no longer matches the command.
        Assert.False(AgentEvidenceGuard.IsAnswerCurrent(
            adoptedVersion, lampBackOn, AgentCorrelation, ownWriteAlreadyAdopted: false, out _, out _));
    }

    [Fact]
    public void ObservedStateMustMatchTheOwnCommand()
    {
        var before = CreateSnapshot();
        var requested = AgentEvidenceGuard.GetEvidenceVersion(before);
        // The command asked for "off" and succeeded, but the lamp reads "on": nothing to adopt.
        var mismatched = WithOwnRestore(before, AgentCorrelation) with
        {
            OperationalState = WithOwnRestore(before, AgentCorrelation).OperationalState with { ReportedIsOn = true }
        };

        Assert.False(AgentEvidenceGuard.IsAnswerCurrent(
            requested, mismatched, AgentCorrelation, ownWriteAlreadyAdopted: false, out _, out _));
    }

    [Fact]
    public void FailedOwnCommandIsNotAdopted()
    {
        var before = CreateSnapshot();
        var requested = AgentEvidenceGuard.GetEvidenceVersion(before);
        var failed = WithOwnRestore(before, AgentCorrelation) with
        {
            OperationalState = WithOwnRestore(before, AgentCorrelation).OperationalState with
            {
                LastCommand = WithOwnRestore(before, AgentCorrelation).OperationalState.LastCommand! with
                {
                    Status = CommandExecutionStatus.Failed
                }
            }
        };

        Assert.False(AgentEvidenceGuard.IsAnswerCurrent(
            requested, failed, AgentCorrelation, ownWriteAlreadyAdopted: false, out _, out _));
    }

    [Fact]
    public void ForeignCommandInvalidatesTheAnswer()
    {
        var before = CreateSnapshot();
        var requested = AgentEvidenceGuard.GetEvidenceVersion(before);
        var after = WithOwnRestore(before, "someone-elses-corr");

        Assert.False(AgentEvidenceGuard.IsAnswerCurrent(
            requested, after, AgentCorrelation, ownWriteAlreadyAdopted: false, out _, out _));
    }

    [Fact]
    public void AmbientChangeInvalidatesTheAnswerEvenWithOwnCommand()
    {
        var before = CreateSnapshot();
        var requested = AgentEvidenceGuard.GetEvidenceVersion(before);
        var after = WithOwnRestore(before, AgentCorrelation) with
        {
            CustomerReport = new CustomerReportRecord(
                "RPT-9", "L-417", "Harbor", "Still glowing!", "img", "App", DateTimeOffset.UtcNow, "new-report-corr")
        };

        Assert.False(AgentEvidenceGuard.IsAnswerCurrent(
            requested, after, AgentCorrelation, ownWriteAlreadyAdopted: false, out _, out _));
    }

    [Fact]
    public void MissingCorrelationFallsBackToStrictComparison()
    {
        var before = CreateSnapshot();
        var requested = AgentEvidenceGuard.GetEvidenceVersion(before);
        var after = WithOwnRestore(before, AgentCorrelation);

        Assert.False(AgentEvidenceGuard.IsAnswerCurrent(
            requested, after, answerCorrelationId: null, ownWriteAlreadyAdopted: false, out _, out _));
        Assert.False(AgentEvidenceGuard.IsAnswerCurrent(
            requested, after, answerCorrelationId: string.Empty, ownWriteAlreadyAdopted: false, out _, out _));
    }

    [Fact]
    public void MissingSnapshotYieldsNullVersion()
    {
        Assert.Null(AgentEvidenceGuard.GetEvidenceVersion(null));
        Assert.True(AgentEvidenceGuard.IsAnswerCurrent(
            null, null, AgentCorrelation, ownWriteAlreadyAdopted: false, out var version, out _));
        Assert.Null(version);
    }

    [Fact]
    public void AStageAdvanceIsReportedAsAStageChange()
    {
        // The operator is told what actually moved. Calling a stage advance an operational change
        // sends the presenter looking at the city for something that never happened.
        var before = CreateSnapshot();
        var after = before with
        {
            CurrentStage = before.CurrentStage with { Id = DemoStage.Workflow, CorrelationId = "stage-corr-2" }
        };

        Assert.Equal(
            AgentEvidenceChange.Stage,
            AgentEvidenceGuard.DescribeChange(
                AgentEvidenceGuard.GetEvidenceVersion(before),
                AgentEvidenceGuard.GetEvidenceVersion(after)));
    }

    [Fact]
    public void AMovedLampIsReportedAsAnOperationalChange()
    {
        var before = CreateSnapshot();
        var after = before with
        {
            OperationalState = before.OperationalState with { ReportedIsOn = false, StateRevision = 8 }
        };

        Assert.Equal(
            AgentEvidenceChange.OperationalState,
            AgentEvidenceGuard.DescribeChange(
                AgentEvidenceGuard.GetEvidenceVersion(before),
                AgentEvidenceGuard.GetEvidenceVersion(after)));
    }

    [Fact]
    public void AnIdenticalVersionReportsNoChange()
    {
        var snapshot = CreateSnapshot();
        var version = AgentEvidenceGuard.GetEvidenceVersion(snapshot);

        Assert.Equal(AgentEvidenceChange.None, AgentEvidenceGuard.DescribeChange(version, version));
    }

    [Fact]
    public void AnUncomparableVersionIsNotGuessedAt()
    {
        Assert.Equal(AgentEvidenceChange.Unknown, AgentEvidenceGuard.DescribeChange(null, "a|b|c|d|e"));
        Assert.Equal(AgentEvidenceChange.Unknown, AgentEvidenceGuard.DescribeChange("truncated", "a|b|c|d|e"));
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
                OperationalContext.None,
                StateRevision: 7),
            new ScenarioStatus(ScenarioId.ForgottenOverride, "Forgotten override", "Test scenario", now, "scenario-corr"),
            CustomerReport: null,
            OpenIncident: null,
            RecentActivity: [],
            new DemoStageStatus(DemoStage.InteractiveInput, "InteractiveInput", "Test stage", ["Test"], now, "stage-corr"));
    }

    // The agent's own restore: the lamp goes off, the override clears, the revision advances.
    private static CommandCenterSnapshot WithOwnRestore(CommandCenterSnapshot snapshot, string correlationId) =>
        snapshot with
        {
            OperationalState = snapshot.OperationalState with
            {
                ReportedIsOn = false,
                DesiredIsOn = false,
                ManualOverride = false,
                StateRevision = snapshot.OperationalState.StateRevision + 1,
                LastCommand = new CommandRecord(
                    "Restore scheduled mode",
                    DesiredIsOn: false,
                    CommandExecutionStatus.Succeeded,
                    correlationId,
                    snapshot.OperationalState.LastReportedAt,
                    snapshot.OperationalState.LastReportedAt.AddSeconds(2),
                    "Restored to scheduled mode.")
            }
        };
}
