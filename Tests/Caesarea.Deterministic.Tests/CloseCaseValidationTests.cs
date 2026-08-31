using OperationsAgent.Api.Services;
using OperationsAgent.Contracts;

namespace Caesarea.Deterministic.Tests;

public sealed class CloseCaseValidationTests
{
    [Theory]
    [InlineData("L-417")]
    [InlineData("POLE-123456")]
    public void AcceptsCanonicalAssetIdentifiers(string assetId)
    {
        Assert.Null(CloseCaseValidation.Validate(new OperationsAgentCloseCaseRequest(assetId, "Symptom", "Resolution")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("417")]
    [InlineData("L-")]
    [InlineData("L-417X")]
    [InlineData("L 417")]
    [InlineData("STREET-1234567")]
    [InlineData("<script>alert(1)</script>")]
    public void RejectsNonCanonicalAssetIdentifiers(string assetId)
    {
        var error = CloseCaseValidation.Validate(new OperationsAgentCloseCaseRequest(assetId, "Symptom", "Resolution"));

        Assert.NotNull(error);
        Assert.Contains("asset identifier", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsBlankOrOverlongSymptom()
    {
        Assert.NotNull(CloseCaseValidation.Validate(new OperationsAgentCloseCaseRequest("L-417", " ", "Resolution")));
        Assert.NotNull(CloseCaseValidation.Validate(new OperationsAgentCloseCaseRequest(
            "L-417", new string('s', CloseCaseValidation.MaxSymptomLength + 1), "Resolution")));
    }

    [Fact]
    public void RejectsBlankOrOverlongResolution()
    {
        Assert.NotNull(CloseCaseValidation.Validate(new OperationsAgentCloseCaseRequest("L-417", "Symptom", " ")));
        Assert.NotNull(CloseCaseValidation.Validate(new OperationsAgentCloseCaseRequest(
            "L-417", "Symptom", new string('r', CloseCaseValidation.MaxResolutionLength + 1))));
    }

    [Fact]
    public void AcceptsFieldsExactlyAtTheirLimits()
    {
        Assert.Null(CloseCaseValidation.Validate(new OperationsAgentCloseCaseRequest(
            "L-417",
            new string('s', CloseCaseValidation.MaxSymptomLength),
            new string('r', CloseCaseValidation.MaxResolutionLength))));
    }
}
