using OperationsAgent.Api.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class InvestigationServiceTests
{
    [Fact]
    public async Task InvestigateAsyncAssemblesResultFromRunnerResponseAndToolTrace()
    {
        var clock = new TestTimeProvider();
        var modelResponse = new ModelInvestigationResponse(
            [new ModelVerifiedFact("Asset is reported on.", OperationsToolset.EnergyAssetStateToolName)],
            [new ModelHypothesis("Manual override is active.", 0.75, "State shows manual override true.")],
            ["No live field confirmation is available."],
            "L-417 is on due to a manual override.");

        var runner = new FakeInvestigationAgentRunner
        {
            Response = modelResponse,
            OnBeforeReturn = static async (toolset, cancellationToken) =>
            {
                await toolset.GetEnergyAssetStateAsync(cancellationToken);
                await toolset.GetIncidentContextAsync(cancellationToken);
            }
        };

        var service = new InvestigationService(
            new FakeEnergyReadGateway { State = CreateState(clock), Activity = [] },
            new FakeCommandCenterReadGateway(),
            runner,
            NullLoggerFactory.Instance,
            "Operations Agent",
            clock,
            NullLogger<InvestigationService>.Instance);

        var result = await service.InvestigateAsync(DemoAssets.StreetlightAssetId, "investigate-corr", CancellationToken.None);

        Assert.Equal(DemoAssets.StreetlightAssetId, result.AssetId);
        Assert.Equal("Operations Agent", result.AgentName);
        Assert.Equal(InvestigationStatus.Completed, result.Status);
        Assert.Equal("investigate-corr", result.CorrelationId);
        Assert.Single(result.VerifiedFacts);
        Assert.Equal(OperationsToolset.EnergyAssetStateToolName, result.VerifiedFacts[0].Source);
        Assert.Single(result.Hypotheses);
        Assert.Equal(0.75, result.Hypotheses[0].Confidence);
        Assert.Single(result.MissingEvidence);
        Assert.Equal("L-417 is on due to a manual override.", result.Summary);
        Assert.Equal(2, result.EvidenceTrace.Count);
        Assert.Equal(OperationsToolset.EnergyAssetStateToolName, result.EvidenceTrace[0].ToolName);
        Assert.Equal(OperationsToolset.IncidentContextToolName, result.EvidenceTrace[1].ToolName);
        Assert.Equal(DemoAssets.StreetlightAssetId, runner.LastAssetId);
        Assert.True(result.CompletedAt >= result.StartedAt);
    }

    [Fact]
    public async Task InvestigateAsyncRejectsInvestigationWithoutAuthoritativeState()
    {
        var clock = new TestTimeProvider();
        var runner = new FakeInvestigationAgentRunner
        {
            Response = new ModelInvestigationResponse([], [], ["Nothing could be verified."], "Insufficient evidence.")
        };
        var service = new InvestigationService(
            new FakeEnergyReadGateway { State = CreateState(clock), Activity = [] },
            new FakeCommandCenterReadGateway(),
            runner,
            NullLoggerFactory.Instance,
            "Operations Agent",
            clock,
            NullLogger<InvestigationService>.Instance);

        await Assert.ThrowsAsync<InvestigationEvidenceUnavailableException>(() =>
            service.InvestigateAsync(DemoAssets.StreetlightAssetId, "empty-trace-corr", CancellationToken.None));
    }

    [Fact]
    public async Task InvestigateAsyncRejectsFactCitingFailedTool()
    {
        var clock = new TestTimeProvider();
        var runner = new FakeInvestigationAgentRunner
        {
            Response = new ModelInvestigationResponse(
                [new ModelVerifiedFact("A report exists.", OperationsToolset.CustomerReportToolName)],
                [],
                [],
                "Evidence reviewed."),
            OnBeforeReturn = static async (toolset, cancellationToken) =>
            {
                await toolset.GetCustomerReportAsync(cancellationToken);
                await toolset.GetEnergyAssetStateAsync(cancellationToken);
            }
        };
        var commandCenterGateway = new FakeCommandCenterReadGateway
        {
            OnGetCustomerReportContextAsync = static (_, _, _) => throw new HttpRequestException("Unavailable.")
        };
        var service = new InvestigationService(
            new FakeEnergyReadGateway { State = CreateState(clock), Activity = [] },
            commandCenterGateway,
            runner,
            NullLoggerFactory.Instance,
            "Operations Agent",
            clock,
            NullLogger<InvestigationService>.Instance);

        await Assert.ThrowsAsync<InvestigationResponseFormatException>(() =>
            service.InvestigateAsync(DemoAssets.StreetlightAssetId, "failed-source-corr", CancellationToken.None));
    }

    [Fact]
    public async Task InvestigateAsyncRejectsBlankAssetId()
    {
        var clock = new TestTimeProvider();
        var runner = new FakeInvestigationAgentRunner
        {
            Response = new ModelInvestigationResponse([], [], [], "n/a")
        };
        var service = new InvestigationService(
            new FakeEnergyReadGateway { State = CreateState(clock), Activity = [] },
            new FakeCommandCenterReadGateway(),
            runner,
            NullLoggerFactory.Instance,
            "Operations Agent",
            clock,
            NullLogger<InvestigationService>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() => service.InvestigateAsync(" ", "corr", CancellationToken.None));
    }

    private static EnergyOperationalTwin CreateState(TestTimeProvider clock) =>
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
            clock.GetUtcNow().AddMinutes(-30),
            true,
            null,
            clock.GetUtcNow(),
            OperationalContext.None);
}
