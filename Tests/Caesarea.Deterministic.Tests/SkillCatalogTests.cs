using Microsoft.Agents.AI;
using OperationsAgent.Api.Services;
using OperationsAgent.Contracts;

namespace Caesarea.Deterministic.Tests;

public sealed class SkillCatalogTests
{
    [Fact]
    public void ResolvesTheRepositorySkillsDirectory()
    {
        var directory = SkillCatalog.ResolveDirectory("skills");

        Assert.NotNull(directory);
        Assert.True(Directory.Exists(directory));
    }

    [Fact]
    public void DescribesTheStreetlightInvestigationSkillFromItsFrontmatter()
    {
        var skills = SkillCatalog.Describe(SkillCatalog.ResolveDirectory("skills"));

        var investigation = Assert.Single(skills, skill => skill.Name == "streetlight-investigation");
        Assert.False(string.IsNullOrWhiteSpace(investigation.Description));
    }

    [Fact]
    public void DescribeReturnsNothingForMissingDirectory()
    {
        Assert.Empty(SkillCatalog.Describe(null));
        Assert.Empty(SkillCatalog.Describe(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
    }

    [Fact]
    public void LoadSkillToolNameMatchesTheSdkProvider()
    {
        // The UI checks tool calls against the contract constant; it must track the SDK's tool name.
        Assert.Equal(AgentSkillsProvider.LoadSkillToolName, OperationsAgentToolNames.LoadSkill);
    }

    [Fact]
    public void SkillFrontmatterSatisfiesTheSdkValidationRules()
    {
        var skills = SkillCatalog.Describe(SkillCatalog.ResolveDirectory("skills"));

        Assert.NotEmpty(skills);

        // AgentSkillsProvider rejects invalid frontmatter at discovery time; catching it here keeps
        // a bad presenter edit from surfacing first during a live run.
        foreach (var skill in skills)
        {
            var frontmatter = new AgentSkillFrontmatter(skill.Name, skill.Description);
            Assert.Equal(skill.Name, frontmatter.Name);
        }
    }
}
