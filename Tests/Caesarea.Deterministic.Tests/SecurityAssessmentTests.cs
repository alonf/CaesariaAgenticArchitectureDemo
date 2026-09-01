using Security.Contracts;
using SecurityAgent.Api.Services;

namespace Caesarea.Deterministic.Tests;

/// <summary>
/// The boundary between the Security domain and every other domain. The Security Agent is
/// instructed to withhold restricted detail, but instructions are requests; these tests pin the
/// structural guarantee that no restricted value can cross even when the model discloses it.
/// </summary>
public sealed class SecurityAssessmentTests
{
    [Fact]
    public void RestrictedDetailNeverCrossesTheBoundary()
    {
        var status = CreateStatusWithOperation();

        // The model has ignored its instructions and named the unit, the officer, and the
        // classification. None of it may reach the asking domain.
        var leaked = """
            {"requiresLighting": true, "untilUtc": null,
             "reason": "NIGHTHAWK-3 is on perimeter watch, authorized by Superintendent R. Bar-On, RESTRICTED."}
            """;

        var assessment = SecurityAssessmentSanitizer.Sanitize(DemoAssets.NorthPromenadeArea, leaked, status);

        Assert.True(assessment.RequiresLighting);
        Assert.True(assessment.DetailsWithheld);
        foreach (var restricted in (string[])["NIGHTHAWK-3", "Bar-On", "RESTRICTED", "SEC-OP-2291", "camera coverage"])
        {
            Assert.DoesNotContain(restricted, assessment.Reason, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ANonSensitiveReasonIsPassedThrough()
    {
        var status = CreateStatusWithOperation();
        var answer = """{"requiresLighting": true, "reason": "An active operation requires lighting until 14:00."}""";

        var assessment = SecurityAssessmentSanitizer.Sanitize(DemoAssets.NorthPromenadeArea, answer, status);

        Assert.Equal("An active operation requires lighting until 14:00.", assessment.Reason);
    }

    [Fact]
    public void TheDecisionComesFromTheRecordsNotTheModel()
    {
        // A model that says "no lighting required" cannot override the authoritative records:
        // the decision and the deadline are computed, the model only supplies wording.
        var status = CreateStatusWithOperation();
        var contradicting = """{"requiresLighting": false, "reason": "Nothing is happening here."}""";

        var assessment = SecurityAssessmentSanitizer.Sanitize(DemoAssets.NorthPromenadeArea, contradicting, status);

        Assert.True(assessment.RequiresLighting);
        Assert.Equal(status.Operations[0].EndsAt, assessment.UntilUtc);
    }

    [Fact]
    public void NoActiveOperationYieldsAPlainNegative()
    {
        var status = new SecurityAreaStatus(DemoAssets.NorthPromenadeArea, [], DateTimeOffset.UtcNow);

        var assessment = SecurityAssessmentSanitizer.Sanitize(DemoAssets.NorthPromenadeArea, answer: null, status);

        Assert.False(assessment.RequiresLighting);
        Assert.Null(assessment.UntilUtc);
        Assert.False(assessment.DetailsWithheld);
        Assert.Contains("No active security operation", assessment.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnUnparseableAnswerFallsBackToASafeReason()
    {
        var status = CreateStatusWithOperation();

        var assessment = SecurityAssessmentSanitizer.Sanitize(
            DemoAssets.NorthPromenadeArea, "I could not produce JSON, but NIGHTHAWK-3 is out there.", status);

        Assert.True(assessment.RequiresLighting);
        Assert.DoesNotContain("NIGHTHAWK-3", assessment.Reason, StringComparison.OrdinalIgnoreCase);
    }

    private static SecurityAreaStatus CreateStatusWithOperation()
    {
        var now = new DateTimeOffset(2026, 9, 1, 11, 0, 0, TimeSpan.Zero);
        return new SecurityAreaStatus(
            DemoAssets.NorthPromenadeArea,
            [
                new SecurityOperationRecord(
                    "SEC-OP-2291",
                    DemoAssets.NorthPromenadeArea,
                    RequiresLighting: true,
                    now.AddHours(-1),
                    now.AddHours(3),
                    Classification: "RESTRICTED",
                    AuthorizedBy: "Superintendent R. Bar-On",
                    UnitCallSign: "NIGHTHAWK-3",
                    Notes: "Perimeter watch along the promenade; lighting required for camera coverage.")
            ],
            now);
    }
}
