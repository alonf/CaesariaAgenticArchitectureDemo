using OperationsAgent.Api.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class OperationsToolsetTests
{
    [Fact]
    public async Task GetCustomerReportAsyncRecordsEvidenceWhenReportExists()
    {
        var recorder = new InvestigationEvidenceRecorder(new TestTimeProvider());
        var commandCenterGateway = new FakeCommandCenterReadGateway
        {
            CustomerReportContext = new CustomerReportContext(
                DemoAssets.StreetlightAssetId,
                new CustomerReportRecord(
                    "REPORT-1",
                    DemoAssets.StreetlightAssetId,
                    DemoAssets.NorthPromenadeArea,
                    "The light is on during the day.",
                    "/images/report.png",
                    "Resident mobile report",
                    DateTimeOffset.UtcNow,
                    "report-corr"))
        };
        var toolset = CreateToolset(recorder, commandCenterReadGateway: commandCenterGateway);

        var summary = await toolset.GetCustomerReportAsync(CancellationToken.None);

        Assert.Contains("REPORT-1", summary, StringComparison.Ordinal);
        var trace = recorder.GetTrace();
        Assert.Single(trace);
        Assert.Equal(OperationsToolset.CustomerReportToolName, trace[0].ToolName);
        Assert.True(trace[0].Succeeded);
        Assert.Equal(1, trace[0].Sequence);
    }

    [Fact]
    public async Task GetCustomerReportAsyncReportsAbsenceWithoutFabricating()
    {
        var recorder = new InvestigationEvidenceRecorder(new TestTimeProvider());
        var toolset = CreateToolset(recorder);

        var summary = await toolset.GetCustomerReportAsync(CancellationToken.None);

        Assert.Contains("No customer report", summary, StringComparison.Ordinal);
        Assert.True(recorder.GetTrace()[0].Succeeded);
    }

    [Fact]
    public async Task GetEnergyAssetStateAsyncRecordsFailureWhenGatewayThrows()
    {
        var recorder = new InvestigationEvidenceRecorder(new TestTimeProvider());
        var energyGateway = new FakeEnergyReadGateway
        {
            State = CreateState(),
            Activity = [],
            OnGetStateAsync = static (_, _, _) => throw new HttpRequestException("Energy Hub unavailable.")
        };
        var toolset = CreateToolset(recorder, energyReadGateway: energyGateway);

        var summary = await toolset.GetEnergyAssetStateAsync(CancellationToken.None);

        Assert.Contains("unavailable", summary, StringComparison.OrdinalIgnoreCase);
        var trace = recorder.GetTrace();
        Assert.Single(trace);
        Assert.False(trace[0].Succeeded);
        Assert.Equal(OperationsToolset.EnergyAssetStateToolName, trace[0].ToolName);
    }

    [Fact]
    public async Task GetEnergyRecentActivityAsyncPropagatesCorrelationIdToGateway()
    {
        var recorder = new InvestigationEvidenceRecorder(new TestTimeProvider());
        string? observedCorrelationId = null;
        var energyGateway = new FakeEnergyReadGateway
        {
            State = CreateState(),
            Activity = [],
            OnGetRecentActivityAsync = (assetId, limit, correlationId, cancellationToken) =>
            {
                observedCorrelationId = correlationId;
                return Task.FromResult<IReadOnlyList<ActivityRecord>>([]);
            }
        };
        var toolset = CreateToolset(recorder, energyReadGateway: energyGateway, correlationId: "trace-corr");

        await toolset.GetEnergyRecentActivityAsync(CancellationToken.None);

        Assert.Equal("trace-corr", observedCorrelationId);
    }

    [Fact]
    public async Task GetIncidentContextAsyncSummarizesOpenIncident()
    {
        var recorder = new InvestigationEvidenceRecorder(new TestTimeProvider());
        var commandCenterGateway = new FakeCommandCenterReadGateway
        {
            IncidentContext = new IncidentContext(
                DemoAssets.StreetlightAssetId,
                new IncidentRecord(
                    "INC-1",
                    DemoAssets.StreetlightAssetId,
                    DemoAssets.NorthPromenadeArea,
                    "Streetlight on during daylight",
                    "Anomaly already acknowledged.",
                    IncidentSeverity.Warning,
                    IncidentStatus.Open,
                    DateTimeOffset.UtcNow,
                    "incident-corr"))
        };
        var toolset = CreateToolset(recorder, commandCenterReadGateway: commandCenterGateway);

        var summary = await toolset.GetIncidentContextAsync(CancellationToken.None);

        Assert.Contains("INC-1", summary, StringComparison.Ordinal);
        Assert.True(recorder.GetTrace()[0].Succeeded);
    }

    [Fact]
    public void ConstructorRejectsBlankAssetIdOrCorrelationId()
    {
        var recorder = new InvestigationEvidenceRecorder(new TestTimeProvider());

        Assert.Throws<ArgumentException>(() => CreateToolset(recorder, assetId: " "));
        Assert.Throws<ArgumentException>(() => CreateToolset(recorder, correlationId: string.Empty));
    }

    [Fact]
    public void EvidenceTraceEntriesAreOrderedBySequence()
    {
        var recorder = new InvestigationEvidenceRecorder(new TestTimeProvider());

        recorder.Record("tool-a", DemoAssets.StreetlightAssetId, "first", true);
        recorder.Record("tool-b", DemoAssets.StreetlightAssetId, "second", true);

        var trace = recorder.GetTrace();

        Assert.Equal(1, trace[0].Sequence);
        Assert.Equal(2, trace[1].Sequence);
    }

    private static OperationsToolset CreateToolset(
        InvestigationEvidenceRecorder recorder,
        IEnergyReadGateway? energyReadGateway = null,
        ICommandCenterReadGateway? commandCenterReadGateway = null,
        string assetId = DemoAssets.StreetlightAssetId,
        string correlationId = "toolset-corr") =>
        new(
            assetId,
            correlationId,
            energyReadGateway ?? new FakeEnergyReadGateway { State = CreateState(), Activity = [] },
            commandCenterReadGateway ?? new FakeCommandCenterReadGateway(),
            recorder,
            NullLogger<OperationsToolset>.Instance);

    private static EnergyOperationalTwin CreateState() =>
        new(
            DemoAssets.StreetlightAssetId,
            DemoAssets.NorthPromenadeArea,
            true,
            true,
            true,
            false,
            true,
            ControllerHealthInfo.Healthy,
            null,
            DateTimeOffset.UtcNow.AddMinutes(-30),
            true,
            null,
            DateTimeOffset.UtcNow,
            OperationalContext.None);
}
