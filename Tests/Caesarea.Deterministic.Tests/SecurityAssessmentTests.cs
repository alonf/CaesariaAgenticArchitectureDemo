using Security.Contracts;
using SecurityAgent.Api.Services;

namespace Caesarea.Deterministic.Tests;

/// <summary>
/// The boundary between the Security domain and every other domain. The consulted agent is
/// instructed to disclose nothing, but instructions are requests; these tests pin the structural
/// guarantee - the model selects from a closed set and never authors text that crosses.
/// </summary>
public sealed class SecurityAssessmentTests
{
    private const string AgentName = "Caesarea Security Operations Agent";

    [Theory]
    // A whole restricted value, the fragments and paraphrases a substring denylist would miss,
    // and an encoded form. None of them can cross, because none of them is forwarded.
    [InlineData("""{"reasonCode":"ActiveOperationRequiresLighting","reason":"NIGHTHAWK-3 is on watch, authorized by Superintendent R. Bar-On, RESTRICTED."}""")]
    [InlineData("""{"reasonCode":"ActiveOperationRequiresLighting","reason":"Bar-On authorized it."}""")]
    [InlineData("""{"reasonCode":"ActiveOperationRequiresLighting","reason":"Nighthawk three is sweeping the promenade for camera coverage."}""")]
    [InlineData("""{"reasonCode":"ActiveOperationRequiresLighting","reason":"Operation 2291 runs under call sign N-3."}""")]
    [InlineData("Sure! The unit NIGHTHAWK-3 needs light. No JSON for you.")]
    public void NoModelAuthoredTextEverCrossesTheBoundary(string answer)
    {
        var status = CreateStatusWithOperation();

        var assessment = SecurityAssessmentSanitizer.Sanitize(DemoAssets.NorthPromenadeArea, answer, status, AgentName);

        Assert.True(assessment.RequiresLighting);
        Assert.True(assessment.DetailsWithheld);
        foreach (var restricted in (string[])
            ["NIGHTHAWK", "Nighthawk", "Bar-On", "RESTRICTED", "SEC-OP-2291", "2291", "camera coverage", "promenade", "N-3"])
        {
            Assert.DoesNotContain(restricted, assessment.Reason, StringComparison.OrdinalIgnoreCase);
        }

        // The public text is one of the templates, not a rewrite of what the model said.
        Assert.StartsWith("An active security operation requires this area to remain lit", assessment.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDecisionAndDeadlineComeFromTheRecordsNotTheModel()
    {
        // A model that says "no operation" cannot switch off security lighting: the floor is
        // deterministic and the contradicting classification is replaced by the supported one.
        var status = CreateStatusWithOperation();
        var contradicting = """{"reasonCode":"NoActiveOperation"}""";

        var assessment = SecurityAssessmentSanitizer.Sanitize(DemoAssets.NorthPromenadeArea, contradicting, status, AgentName);

        Assert.True(assessment.RequiresLighting);
        Assert.Equal(SecurityLightingReason.ActiveOperationRequiresLighting, assessment.ReasonCode);
        Assert.Equal(status.Operations[0].EndsAt, assessment.UntilUtc);
    }

    [Fact]
    public void AnAgreeingClassificationIsKept()
    {
        var status = CreateStatusWithOperation();
        var agreeing = """{"reasonCode":"ActiveOperationRequiresLighting"}""";

        var assessment = SecurityAssessmentSanitizer.Sanitize(DemoAssets.NorthPromenadeArea, agreeing, status, AgentName);

        Assert.Equal(SecurityLightingReason.ActiveOperationRequiresLighting, assessment.ReasonCode);
        Assert.Equal(AgentName, assessment.AssessedBy);
    }

    [Fact]
    public void AnActiveOperationThatDoesNotNeedLightingIsDistinguished()
    {
        var status = CreateStatusWithOperation(requiresLighting: false);

        var assessment = SecurityAssessmentSanitizer.Sanitize(
            DemoAssets.NorthPromenadeArea, """{"reasonCode":"ActiveOperationWithoutLightingRequirement"}""", status, AgentName);

        Assert.False(assessment.RequiresLighting);
        Assert.Null(assessment.UntilUtc);
        Assert.Equal(SecurityLightingReason.ActiveOperationWithoutLightingRequirement, assessment.ReasonCode);
        // An operation is still active, so the fact that detail exists is itself disclosed.
        Assert.True(assessment.DetailsWithheld);
    }

    [Fact]
    public void NoActiveOperationYieldsAPlainNegative()
    {
        var status = new SecurityAreaStatus(DemoAssets.NorthPromenadeArea, [], DateTimeOffset.UtcNow);

        var assessment = SecurityAssessmentSanitizer.Sanitize(DemoAssets.NorthPromenadeArea, answer: null, status, AgentName);

        Assert.False(assessment.RequiresLighting);
        Assert.Null(assessment.UntilUtc);
        Assert.False(assessment.DetailsWithheld);
        Assert.Equal(SecurityLightingReason.NoActiveOperation, assessment.ReasonCode);
    }

    [Theory]
    [InlineData("""{"reasonCode":42}""")]
    [InlineData("""{"reasonCode":{"nested":"object"}}""")]
    [InlineData("""{"reasonCode":"NotARealCode"}""")]
    [InlineData("""{"unterminated": """)]
    public void MalformedClassificationsFallBackToTheRecords(string answer)
    {
        var status = CreateStatusWithOperation();

        var assessment = SecurityAssessmentSanitizer.Sanitize(DemoAssets.NorthPromenadeArea, answer, status, AgentName);

        Assert.Equal(SecurityLightingReason.ActiveOperationRequiresLighting, assessment.ReasonCode);
        Assert.True(assessment.RequiresLighting);
    }

    [Fact]
    public void TheRestrictedToolServesOnlyTheAreaUnderAssessment()
    {
        // The model asking about a different area must not receive another area's records.
        var status = CreateStatusWithOperation();
        var tools = new SecurityAreaTools(
            DemoAssets.NorthPromenadeArea, status, "scope-corr", NullLogger<SecurityAreaTools>.Instance);

        Assert.Same(status, tools.GetAreaSecurityOperations(DemoAssets.NorthPromenadeArea));
        Assert.Same(status, tools.GetAreaSecurityOperations("north promenade"));

        var refused = Assert.Throws<ArgumentException>(() => tools.GetAreaSecurityOperations("Harbor District"));
        Assert.Contains("out of scope", refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheToolServesTheSameSnapshotTheResultIsComputedFrom()
    {
        // One read, one snapshot: the agent cannot be shown records that the sanitizer did not
        // also see, which is what makes "nothing undisclosed can leak" true by construction.
        var status = CreateStatusWithOperation();
        var tools = new SecurityAreaTools(
            DemoAssets.NorthPromenadeArea, status, "snapshot-corr", NullLogger<SecurityAreaTools>.Instance);

        Assert.Same(status, tools.GetAreaSecurityOperations(DemoAssets.NorthPromenadeArea));
        Assert.Same(status, tools.GetAreaSecurityOperations(DemoAssets.NorthPromenadeArea));
    }

    private static SecurityAreaStatus CreateStatusWithOperation(bool requiresLighting = true)
    {
        var now = new DateTimeOffset(2026, 9, 1, 11, 0, 0, TimeSpan.Zero);
        return new SecurityAreaStatus(
            DemoAssets.NorthPromenadeArea,
            [
                new SecurityOperationRecord(
                    "SEC-OP-2291",
                    DemoAssets.NorthPromenadeArea,
                    requiresLighting,
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
