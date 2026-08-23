using OperationsAgent.Api.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class InvestigationResponseParserTests
{
    [Fact]
    public void ParseAcceptsAWellFormedResponse()
    {
        const string json = """
            {
              "verifiedFacts": [{ "statement": "Asset L-417 is reported on.", "source": "get_energy_asset_state" }],
              "hypotheses": [{ "statement": "Manual override is active.", "confidence": 0.8, "reason": "State shows manual override true." }],
              "missingEvidence": ["No live field confirmation is available."],
              "summary": "L-417 is on due to a manual override."
            }
            """;

        var result = InvestigationResponseParser.Parse(json);

        Assert.Single(result.VerifiedFacts);
        Assert.Single(result.Hypotheses);
        Assert.Single(result.MissingEvidence);
        Assert.Equal(0.8, result.Hypotheses[0].Confidence);
        Assert.Equal("L-417 is on due to a manual override.", result.Summary);
    }

    [Fact]
    public void ParseAcceptsEmptyEvidenceCollections()
    {
        const string json = """{"verifiedFacts":[],"hypotheses":[],"missingEvidence":[],"summary":"No evidence was available."}""";

        var result = InvestigationResponseParser.Parse(json);

        Assert.Empty(result.VerifiedFacts);
        Assert.Empty(result.Hypotheses);
        Assert.Empty(result.MissingEvidence);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseRejectsEmptyResponses(string? rawJson)
    {
        Assert.Throws<InvestigationResponseFormatException>(() => InvestigationResponseParser.Parse(rawJson));
    }

    [Fact]
    public void ParseRejectsMalformedJson()
    {
        var exception = Assert.Throws<InvestigationResponseFormatException>(() => InvestigationResponseParser.Parse("{not-json"));
        Assert.IsType<System.Text.Json.JsonException>(exception.InnerException);
    }

    [Fact]
    public void ParseRejectsMissingSummary()
    {
        const string json = """{"verifiedFacts":[],"hypotheses":[],"missingEvidence":[],"summary":""}""";

        Assert.Throws<InvestigationResponseFormatException>(() => InvestigationResponseParser.Parse(json));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void ParseRejectsHypothesisConfidenceOutsideZeroToOneRange(double confidence)
    {
        var json = $$"""
            {
              "verifiedFacts": [],
              "hypotheses": [{ "statement": "x", "confidence": {{confidence}}, "reason": "y" }],
              "missingEvidence": [],
              "summary": "s"
            }
            """;

        Assert.Throws<InvestigationResponseFormatException>(() => InvestigationResponseParser.Parse(json));
    }

    [Fact]
    public void ParseRejectsBlankMissingEvidenceEntries()
    {
        const string json = """{"verifiedFacts":[],"hypotheses":[],"missingEvidence":["   "],"summary":"s"}""";

        Assert.Throws<InvestigationResponseFormatException>(() => InvestigationResponseParser.Parse(json));
    }

    [Fact]
    public void ParseRejectsVerifiedFactMissingSource()
    {
        const string json = """{"verifiedFacts":[{"statement":"x","source":""}],"hypotheses":[],"missingEvidence":[],"summary":"s"}""";

        Assert.Throws<InvestigationResponseFormatException>(() => InvestigationResponseParser.Parse(json));
    }

    [Fact]
    public void ParseRejectsVerifiedFactWithUnknownToolSource()
    {
        const string json = """{"verifiedFacts":[{"statement":"x","source":"Energy Hub"}],"hypotheses":[],"missingEvidence":[],"summary":"s"}""";

        Assert.Throws<InvestigationResponseFormatException>(() => InvestigationResponseParser.Parse(json));
    }
}
