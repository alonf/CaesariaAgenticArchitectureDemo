using OperationsAgent.Api.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class EnergyToolsTests
{
    [Fact]
    public async Task GetStreetlightStateAsyncReadsAuthoritativeStateAndPropagatesCorrelationId()
    {
        string? observedAssetId = null;
        string? observedCorrelationId = null;
        var expected = CreateState();
        var gateway = new FakeEnergyReadGateway
        {
            State = expected,
            Activity = [],
            OnGetStateAsync = (assetId, correlationId, cancellationToken) =>
            {
                observedAssetId = assetId;
                observedCorrelationId = correlationId;
                return Task.FromResult(expected);
            }
        };
        var tools = new EnergyTools(gateway, "agent-corr", NullLogger<EnergyTools>.Instance);

        var result = await tools.GetStreetlightStateAsync(DemoAssets.StreetlightAssetId, CancellationToken.None);

        Assert.Equal(expected, result);
        Assert.Equal(DemoAssets.StreetlightAssetId, observedAssetId);
        Assert.Equal("agent-corr", observedCorrelationId);
    }

    [Fact]
    public async Task GetStreetlightStateAsyncRejectsBlankAssetId()
    {
        var tools = new EnergyTools(
            new FakeEnergyReadGateway { State = CreateState(), Activity = [] },
            "agent-corr",
            NullLogger<EnergyTools>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.GetStreetlightStateAsync(" ", CancellationToken.None));
    }

    [Fact]
    public void ConstructorRejectsBlankCorrelationId()
    {
        Assert.Throws<ArgumentException>(() =>
            new EnergyTools(
                new FakeEnergyReadGateway { State = CreateState(), Activity = [] },
                " ",
                NullLogger<EnergyTools>.Instance));
    }

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
