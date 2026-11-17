using OperationsAgent.Api.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class EnergyToolsTests
{
    [Fact]
    public async Task SharedHostedToolsKeepConcurrentRequestCorrelationsSeparate()
    {
        var accessor = new Microsoft.AspNetCore.Http.HttpContextAccessor();
        var seen = new System.Collections.Concurrent.ConcurrentBag<string>();
        var gateway = new FakeEnergyReadGateway
        {
            State = CreateState(),
            Activity = [],
            OnGetStateAsync = (_, correlationId, _) =>
            {
                seen.Add(correlationId);
                return Task.FromResult(CreateState());
            }
        };
        var tools = new EnergyTools(gateway,
            () => InvocationCorrelation.Resolve(accessor.HttpContext), NullLogger<EnergyTools>.Instance);

        async Task InvokeAsync(string correlationId)
        {
            accessor.HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
            accessor.HttpContext.Request.Headers[CorrelationIds.HeaderName] = correlationId;
            await Task.Yield();
            await tools.GetStreetlightStateAsync(DemoAssets.StreetlightAssetId, TestContext.Current.CancellationToken);
        }

        await Task.WhenAll(InvokeAsync("operator-a"), InvokeAsync("operator-b"));
        Assert.Equal(["operator-a", "operator-b"], seen.Order(StringComparer.Ordinal));
    }

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
