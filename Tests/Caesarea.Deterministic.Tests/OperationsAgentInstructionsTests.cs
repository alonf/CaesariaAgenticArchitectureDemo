using OperationsAgent.Api.Services;

namespace Caesarea.Deterministic.Tests;

/// <summary>
/// Pins the hosted habitat's instruction composition. The Work IQ boundary is the only thing
/// standing between "invoke action tools immediately" and a delegated permission that can write to
/// someone's Microsoft 365, so its presence - and its absence when the toolbox is not part of the
/// composition - is a contract, not an implementation detail.
/// </summary>
public sealed class OperationsAgentInstructionsTests
{
    [Fact]
    public void WithWorkIqTheBoundaryFollowsTheSharedCore()
    {
        var composed = OperationsAgentInstructions.ComposeForHostedHabitat(workIqToolboxRegistered: true);

        Assert.StartsWith(OperationsAgentInstructions.Text, composed, StringComparison.Ordinal);
        Assert.EndsWith(OperationsAgentInstructions.WorkIqEvidenceOnlyBoundary, composed, StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutWorkIqTheSharedCoreIsUsedVerbatim()
    {
        // No toolbox, no boundary: an instruction about a tool the agent does not carry would
        // read as noise and imply a capability the composition does not have.
        Assert.Equal(
            OperationsAgentInstructions.Text,
            OperationsAgentInstructions.ComposeForHostedHabitat(workIqToolboxRegistered: false));
    }

    [Theory]
    [InlineData("create")]
    [InlineData("modify")]
    [InlineData("send")]
    [InlineData("delete")]
    [InlineData("even when explicitly asked")]
    public void TheBoundaryForbidsEveryWriteVerb(string phrase)
    {
        // The permission allows actions, so the boundary must name them all; losing one to a
        // rewording quietly reopens it.
        var boundary = OperationsAgentInstructions.WorkIqEvidenceOnlyBoundary;

        Assert.Contains(phrase, boundary, StringComparison.OrdinalIgnoreCase);
    }
}
