using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
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
    public async Task DescribesTheStreetlightInvestigationSkillThroughSdkDiscovery()
    {
        // Describe delegates to the SDK's own AgentFileSkillsSource, so the UI trace reports
        // exactly what the AgentSkillsProvider would advertise.
        var skills = await SkillCatalog.DescribeAsync(
            SkillCatalog.ResolveDirectory("skills"), CreateProbeAgent(), NullLoggerFactory.Instance, TestContext.Current.CancellationToken);

        var investigation = Assert.Single(skills, skill => skill.Name == "streetlight-investigation");
        Assert.False(string.IsNullOrWhiteSpace(investigation.Description));
    }

    [Fact]
    public async Task DescribeReturnsNothingForMissingDirectory()
    {
        Assert.Empty(await SkillCatalog.DescribeAsync(null, CreateProbeAgent(), NullLoggerFactory.Instance, TestContext.Current.CancellationToken));
        Assert.Empty(await SkillCatalog.DescribeAsync(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), CreateProbeAgent(), NullLoggerFactory.Instance, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DescribeSkipsFilesWithoutAFrontmatterBlock()
    {
        // Field lines outside a --- block are not frontmatter; SDK discovery skips such a file,
        // and Describe reports whatever the SDK decided.
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "broken-skill"));

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(directory.FullName, "SKILL.md"),
                "name: broken-skill\ndescription: No delimiters around these fields.\n# Body",
                TestContext.Current.CancellationToken);

            Assert.Empty(await SkillCatalog.DescribeAsync(
                directory.Parent!.FullName, CreateProbeAgent(), NullLoggerFactory.Instance, TestContext.Current.CancellationToken));
        }
        finally
        {
            directory.Parent!.Delete(recursive: true);
        }
    }

    [Fact]
    public void LoadSkillToolNameMatchesTheSdkProvider()
    {
        // The UI checks tool calls against the contract constant; it must track the SDK's tool name.
        Assert.Equal(AgentSkillsProvider.LoadSkillToolName, OperationsAgentToolNames.LoadSkill);
    }

    [Theory]
    [InlineData("""{"skillName":"streetlight-investigation"}""", true)]
    [InlineData("""{"skillName":"streetlight-investigation-typo"}""", false)]
    [InlineData("""{"skillName":"Streetlight-Investigation"}""", false)]
    [InlineData("""{"other":"streetlight-investigation"}""", false)]
    [InlineData("not json", false)]
    public void LoadedDetectionRequiresTheExactSkillName(string argumentsJson, bool expected)
    {
        // The SDK performs an exact lookup, so the trace must not over-report via substring or
        // case-insensitive matches.
        Assert.Equal(expected, SkillCatalog.IsLoadSkillCallFor(argumentsJson, "streetlight-investigation"));
    }

    [Fact]
    public async Task SdkProviderAdvertisesTheSkillAndExposesLoadSkill()
    {
        // The real AgentSkillsProvider against the real repository skill, driven through a
        // deterministic fake chat client: proves the SDK discovers the file, advertises its
        // name/description in the instructions, and contributes the load_skill tool.
        var capturingClient = new CapturingChatClient();
        using var skills = new AgentSkillsProvider(
            SkillCatalog.ResolveDirectory("skills")!,
            options: new AgentSkillsProviderOptions { DisableLoadSkillApproval = true });
        var agent = new ChatClientAgent(capturingClient, new ChatClientAgentOptions
        {
            AIContextProviders = [skills]
        });

        await agent.RunAsync("Investigate streetlight L-417.", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("streetlight-investigation", capturingClient.LastInstructions, StringComparison.Ordinal);
        Assert.Contains(
            capturingClient.LastToolNames,
            toolName => toolName == AgentSkillsProvider.LoadSkillToolName);
    }

    [Fact]
    public async Task ShippedSkillPassesSdkDiscoveryValidation()
    {
        // Discovery IS the SDK's validation: a bad presenter edit fails here, not on stage.
        var skills = await SkillCatalog.DescribeAsync(
            SkillCatalog.ResolveDirectory("skills"), CreateProbeAgent(), NullLoggerFactory.Instance, TestContext.Current.CancellationToken);

        Assert.NotEmpty(skills);
    }

    [Fact]
    public void ShippedSkillPinsTheProcedureAndContainsNoSecrets()
    {
        var skillPath = Path.Combine(SkillCatalog.ResolveDirectory("skills")!, "streetlight-investigation", "SKILL.md");
        var content = File.ReadAllText(skillPath);

        // The lecture beat depends on these sections; a presenter edit that removes one should
        // fail here, not on stage.
        Assert.Contains("Verify live state", content, StringComparison.Ordinal);
        Assert.Contains("Search for evidence", content, StringComparison.Ordinal);
        Assert.Contains("Consider recalled cases", content, StringComparison.Ordinal);
        Assert.Contains("Triage guide", content, StringComparison.Ordinal);
        Assert.Contains("Caesarea Incident Brief", content, StringComparison.Ordinal);
        Assert.Contains("Procedure: streetlight-investigation v1", content, StringComparison.Ordinal);

        // Skill content is injected into model instructions; it must never carry credentials.
        foreach (var forbidden in new[] { "password", "api key", "apikey", "api_key", "secret", "connectionstring", "bearer " })
        {
            Assert.DoesNotContain(forbidden, content, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static ChatClientAgent CreateProbeAgent() =>
        new(new CapturingChatClient(), new ChatClientAgentOptions());

    private sealed class CapturingChatClient : IChatClient
    {
        public string LastInstructions { get; private set; } = string.Empty;

        public IReadOnlyList<string> LastToolNames { get; private set; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            LastInstructions = options?.Instructions ?? string.Empty;
            LastToolNames = [.. (options?.Tools ?? []).Select(tool => tool.Name)];
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
            // Nothing to release.
        }
    }
}
