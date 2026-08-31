using OperationsAgent.Api.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class InMemoryCaseMemoryStoreTests
{
    [Fact]
    public void RecordAssignsSequentialCaseIds()
    {
        var store = CreateStore();

        var first = store.Record("L-417", "On during daylight", "Maintenance override, WO-8732.");
        var second = store.Record("L-528", "On during daylight", "Unconfirmed.");

        Assert.Equal("CASE-1", first.CaseId);
        Assert.Equal("CASE-2", second.CaseId);
        Assert.Equal(2, store.GetAll().Count);
    }

    [Fact]
    public void RecallMatchesSimilarSymptomAcrossAssets()
    {
        var store = CreateStore();
        store.Record("L-417", "Streetlight reported on during daylight against its schedule.", "Maintenance override, WO-8732.");

        var recalled = store.Recall("Why is streetlight L-528 on during daylight?");

        Assert.Single(recalled);
        Assert.Equal("CASE-1", recalled[0].CaseId);
    }

    [Fact]
    public void RecallReturnsNothingForUnrelatedQuestion()
    {
        var store = CreateStore();
        store.Record("L-417", "Streetlight reported on during daylight against its schedule.", "Maintenance override, WO-8732.");

        Assert.Empty(store.Recall("What is the harbor crane inventory?"));
    }

    [Fact]
    public void ClearRemovesAllCases()
    {
        var store = CreateStore();
        store.Record("L-417", "Streetlight on during daylight.", "Override.");

        store.Clear();

        Assert.Empty(store.GetAll());
        Assert.Empty(store.Recall("Why is the streetlight on?"));
    }

    private static InMemoryCaseMemoryStore CreateStore() =>
        new(new TestTimeProvider(), NullLogger<InMemoryCaseMemoryStore>.Instance);
}
