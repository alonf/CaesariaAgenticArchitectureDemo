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
    public void RecallIgnoresIncidentalSubstringsAndGenericWords()
    {
        var store = CreateStore();
        store.Record("L-417", "Streetlight on during daylight against its schedule.", "Maintenance override, WO-8732.");

        // "on" inside "controller"/"consumption" and generic words must not recall a lighting case.
        Assert.Empty(store.Recall("What is the controller status?"));
        Assert.Empty(store.Recall("What is the district power consumption?"));
        // A single shared meaningful term is not enough - two or more concepts must match.
        Assert.Empty(store.Recall("List every streetlight in the city."));
    }

    [Fact]
    public void RecallReturnsOnlyTheStrongestMatches()
    {
        var clock = new TestTimeProvider();
        var store = new InMemoryCaseMemoryStore(clock, NullLogger<InMemoryCaseMemoryStore>.Instance);

        for (var i = 0; i < 5; i++)
        {
            store.Record($"L-40{i}", "Streetlight on during daylight against its schedule.", "Override.");
            clock.Advance(TimeSpan.FromMinutes(1));
        }

        var recalled = store.Recall("Why is streetlight L-528 on during daylight against the schedule?");

        Assert.Equal(3, recalled.Count);
        // Newest first among equal-strength matches.
        Assert.Equal("L-404", recalled[0].AssetId);
    }

    [Fact]
    public void RecordTruncatesOverlongFields()
    {
        var store = CreateStore();

        var closedCase = store.Record("L-417", new string('s', 400), new string('r', 5000));

        Assert.Equal(200, closedCase.Symptom.Length);
        Assert.Equal(1000, closedCase.Resolution.Length);
    }

    [Fact]
    public void ClearRemovesAllCasesAndRestartsNumbering()
    {
        var store = CreateStore();
        store.Record("L-417", "Streetlight on during daylight.", "Override.");

        store.Clear();

        Assert.Empty(store.GetAll());
        Assert.Empty(store.Recall("Why is the streetlight on during daylight?"));
        // The scripted lecture beat expects CASE-1 after a rehearsal reset.
        Assert.Equal("CASE-1", store.Record("L-417", "Streetlight on during daylight.", "Override.").CaseId);
    }

    private static InMemoryCaseMemoryStore CreateStore() =>
        new(new TestTimeProvider(), NullLogger<InMemoryCaseMemoryStore>.Instance);
}
