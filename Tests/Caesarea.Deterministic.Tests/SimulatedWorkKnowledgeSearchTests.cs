using OperationsAgent.Api.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class SimulatedWorkKnowledgeSearchTests
{
    [Fact]
    public async Task StreetlightQueryReturnsSeededEvidence()
    {
        var search = CreateSearch();

        var evidence = await search.SearchAsync("Why is streetlight L-417 on during the day?", "wk-corr", CancellationToken.None);

        Assert.Contains(evidence, item => item.Id == "WO-8732");
        Assert.Contains(evidence, item => item.SourceType == "Technician note"
            && item.Summary.Contains("post-maintenance verification", StringComparison.OrdinalIgnoreCase));
        Assert.All(evidence, item => Assert.Equal("Simulated work knowledge", item.SourceLabel));
    }

    [Fact]
    public async Task UnrelatedQueryReturnsNoEvidence()
    {
        var search = CreateSearch();

        var evidence = await search.SearchAsync("harbor cranes inventory", "wk-corr", CancellationToken.None);

        Assert.Empty(evidence);
    }

    [Fact]
    public async Task QueryNamingAnotherAssetReturnsNoEvidence()
    {
        var search = CreateSearch();

        // The seeded evidence is about L-417; handing it out for L-528 would let the agent cite
        // another asset's work order as if it explained this one.
        var evidence = await search.SearchAsync("Why is streetlight L-528 on during daylight?", "wk-corr", CancellationToken.None);

        Assert.Empty(evidence);
    }

    [Fact]
    public async Task WithheldEvidenceReturnsNothingForMatchingQueries()
    {
        var search = CreateSearch();
        search.EvidencePresent = false;

        var evidence = await search.SearchAsync("maintenance on streetlight L-417", "wk-corr", CancellationToken.None);

        Assert.Empty(evidence);
    }

    private static SimulatedWorkKnowledgeSearch CreateSearch() =>
        new(new TestTimeProvider(), NullLogger<SimulatedWorkKnowledgeSearch>.Instance);
}
