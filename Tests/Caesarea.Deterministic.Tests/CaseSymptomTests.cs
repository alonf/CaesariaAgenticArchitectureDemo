using CommandCenter.Web.Services;
using OperationsAgent.Api.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class CaseSymptomTests
{
    // The Memory beat's second question, exactly as the Command Center's button sends it.
    private const string SecondAssetQuestion = "Why is streetlight L-528 on during daylight?";

    [Fact]
    public void TheClosedDaylightAnomalyIsRecalledByTheSecondAssetQuestion()
    {
        // The store recalls by shared meaningful terms, so the symptom the page records and the
        // question the page asks must be phrased in the same words - or the beat shows nothing.
        var store = new InMemoryCaseMemoryStore(new TestTimeProvider(), NullLogger<InMemoryCaseMemoryStore>.Instance);
        store.Record("L-417", CaseSymptom.Describe("L-417", Twin(reportedIsOn: true, manualOverride: true)), "Maintenance override, WO-8732.");

        var recalled = Assert.Single(store.Recall(SecondAssetQuestion));

        Assert.Equal("L-417", recalled.AssetId);
    }

    [Fact]
    public void TheSymptomSaysWhatTheSnapshotShowed()
    {
        Assert.Equal(
            "Streetlight L-417 on during daylight against its schedule, under a manual override.",
            CaseSymptom.Describe("L-417", Twin(reportedIsOn: true, manualOverride: true)));
        Assert.Equal(
            "Streetlight L-417 off during daylight, matching its schedule.",
            CaseSymptom.Describe("L-417", Twin(reportedIsOn: false, manualOverride: false)));
        Assert.Equal(
            "Streetlight L-417 on during daylight as an active operational context required.",
            CaseSymptom.Describe("L-417", Twin(reportedIsOn: true, manualOverride: false, requiresLighting: true)));
        Assert.Equal(
            "Streetlight L-417 off after dark against its schedule, with no manual override.",
            CaseSymptom.Describe("L-417", Twin(reportedIsOn: false, manualOverride: false, isDaylight: false)));
    }

    private static EnergyOperationalTwin Twin(bool reportedIsOn, bool manualOverride, bool requiresLighting = false, bool isDaylight = true) =>
        new(
            "L-417",
            "Harbor Promenade",
            reportedIsOn,
            DesiredIsOn: reportedIsOn,
            isDaylight,
            ExpectedScheduledState: !isDaylight,
            manualOverride,
            ControllerHealthInfo.Healthy,
            LastCommand: null,
            LastMaintenanceTime: null,
            HasRecentMaintenance: false,
            OpenIncidentId: null,
            LastReportedAt: DateTimeOffset.UnixEpoch,
            requiresLighting ? new OperationalContext(true, "Security operation requires lighting.") : OperationalContext.None);
}
