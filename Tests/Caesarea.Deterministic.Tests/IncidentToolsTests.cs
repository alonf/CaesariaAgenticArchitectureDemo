using OperationsAgent.Api.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class IncidentToolsTests
{
    private static readonly DateTimeOffset Opened = new(2026, 9, 6, 0, 14, 0, TimeSpan.Zero);

    [Fact]
    public async Task ATrackedIncidentIsDescribedWithWhatItAlreadyCovers()
    {
        var world = new IncidentWorld();
        world.Incidents.Known["INC-L417-001"] = new IncidentRecord(
            "INC-L417-001",
            DemoAssets.StreetlightAssetId,
            DemoAssets.NorthPromenadeArea,
            "Controller fault on L-417",
            "Controller fault confirmed; a technician dispatch has already been requested under this incident.",
            IncidentSeverity.Warning,
            IncidentStatus.Open,
            Opened,
            "incident-seed");

        var answer = await world.Tools.GetIncidentAsync("  INC-L417-001  ", CancellationToken.None);

        // Everything the model needs to decide not to file: identity, status, and the fact that a
        // dispatch is already pending.
        Assert.Contains("INC-L417-001", answer, StringComparison.Ordinal);
        Assert.Contains("Open", answer, StringComparison.Ordinal);
        Assert.Contains("L-417", answer, StringComparison.Ordinal);
        Assert.Contains("technician dispatch has already been requested", answer, StringComparison.Ordinal);
        Assert.Equal("incident-tool-corr", world.Incidents.LastCorrelationId);
        Assert.Equal("INC-L417-001", world.Incidents.LastRequestedId);
    }

    [Fact]
    public async Task AnUnknownIncidentIsAnsweredNotThrown()
    {
        var world = new IncidentWorld();

        var answer = await world.Tools.GetIncidentAsync("INC-L417-999", CancellationToken.None);

        Assert.Contains("no incident INC-L417-999", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARunawayIdentifierNeverReachesTheCommandCenter()
    {
        var world = new IncidentWorld();

        var answer = await world.Tools.GetIncidentAsync(new string('x', 65), CancellationToken.None);

        Assert.Contains("not a Caesarea incident identifier", answer, StringComparison.Ordinal);
        Assert.Null(world.Incidents.LastRequestedId);
    }

    [Fact]
    public async Task AnIdentifierThatIsOnlyWhitespaceIsRefused()
    {
        var world = new IncidentWorld();

        await Assert.ThrowsAsync<ArgumentException>(() => world.Tools.GetIncidentAsync("   ", CancellationToken.None));
        Assert.Null(world.Incidents.LastRequestedId);
    }

    private sealed class IncidentWorld
    {
        public IncidentWorld()
        {
            Incidents = new FakeIncidentGateway();
            Tools = new IncidentTools(Incidents, "incident-tool-corr", NullLogger<IncidentTools>.Instance);
        }

        public FakeIncidentGateway Incidents { get; }

        public IncidentTools Tools { get; }
    }
}

/// <summary>
/// An incident gateway the tests seed directly; shared with the agent-composition tests, which
/// need a gateway that exists but is never consulted.
/// </summary>
internal sealed class FakeIncidentGateway : IIncidentGateway
{
    public Dictionary<string, IncidentRecord> Known { get; } = new(StringComparer.Ordinal);

    public string? LastRequestedId { get; private set; }

    public string? LastCorrelationId { get; private set; }

    public Task<IncidentRecord?> GetAsync(string incidentId, string correlationId, CancellationToken cancellationToken)
    {
        LastRequestedId = incidentId;
        LastCorrelationId = correlationId;
        return Task.FromResult(Known.GetValueOrDefault(incidentId));
    }
}
