using System.Reflection;
using System.Text.RegularExpressions;

namespace Caesarea.Deterministic.Tests;

public sealed class DemoBreakpointsTests
{
    [Fact]
    public void SnippetIdentifiersAreStableSemanticValues()
    {
        var snippetValues = GetDemoSnippetValues();

        Assert.Equal(
            ["H08_AGENT_CREATION", "H08_AGENT_SESSION", "H08_FUNCTION_TOOL", "H08_KNOWLEDGE_RETRIEVAL"],
            snippetValues.OrderBy(value => value, StringComparer.Ordinal));

        // Slide position is presentation metadata, not identity: no snippet identifier may embed a
        // physical slide number (the retired H08_S13_AGENT style).
        foreach (var value in snippetValues)
        {
            Assert.DoesNotMatch(@"_S\d+(_|$)", value);
        }
    }

    [Fact]
    public void SnippetIdentifiersMatchTheirSourceRegions()
    {
        var repositoryRoot = FindRepositoryRoot();
        var regionNames = Directory
            .GetFiles(Path.Combine(repositoryRoot, "Services"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), @"#region\s+(H08_\w+)").Select(match => match.Groups[1].Value))
            .ToList();

        // Every snippet the registry names has exactly one exported source region, and every
        // exported region is registered - the two lists cannot drift apart.
        Assert.Equal(
            GetDemoSnippetValues().OrderBy(value => value, StringComparer.Ordinal),
            regionNames.OrderBy(value => value, StringComparer.Ordinal));
    }

    private static List<string> GetDemoSnippetValues() =>
        [.. typeof(DemoSnippets)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)];

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Caesarea.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root could not be located from the test output directory.");
    }

    [Fact]
    public void RegisterAddsSnippetsDisarmed()
    {
        DemoBreakpoints.Register("TEST_REGISTER_A", "TEST_REGISTER_B");

        var status = DemoBreakpoints.GetStatus();

        Assert.Contains(status, snippet => snippet is { SnippetName: "TEST_REGISTER_A", IsArmed: false });
        Assert.Contains(status, snippet => snippet is { SnippetName: "TEST_REGISTER_B", IsArmed: false });
    }

    [Fact]
    public void TrySetArmedRejectsUnknownSnippet()
    {
        Assert.False(DemoBreakpoints.TrySetArmed("TEST_UNKNOWN_SNIPPET", armed: true));
    }

    [Fact]
    public void TrySetArmedArmsRegisteredSnippet()
    {
        DemoBreakpoints.Register("TEST_ARM");

        Assert.True(DemoBreakpoints.TrySetArmed("TEST_ARM", armed: true));
        Assert.Contains(DemoBreakpoints.GetStatus(), snippet => snippet is { SnippetName: "TEST_ARM", IsArmed: true });

        Assert.True(DemoBreakpoints.TrySetArmed("TEST_ARM", armed: false));
        Assert.Contains(DemoBreakpoints.GetStatus(), snippet => snippet is { SnippetName: "TEST_ARM", IsArmed: false });
    }

    [Fact]
    public void TryConsumeIsOneShot()
    {
        DemoBreakpoints.Register("TEST_CONSUME");
        DemoBreakpoints.TrySetArmed("TEST_CONSUME", armed: true);

        Assert.True(DemoBreakpoints.TryConsume("TEST_CONSUME"));
        Assert.False(DemoBreakpoints.TryConsume("TEST_CONSUME"));
        Assert.Contains(DemoBreakpoints.GetStatus(), snippet => snippet is { SnippetName: "TEST_CONSUME", IsArmed: false });
    }

    [Fact]
    public void TryConsumeReturnsFalseWhenDisarmed()
    {
        DemoBreakpoints.Register("TEST_DISARMED");

        Assert.False(DemoBreakpoints.TryConsume("TEST_DISARMED"));
    }

    [Fact]
    public void PauseWithoutDebuggerLeavesSnippetArmed()
    {
        DemoBreakpoints.Register("TEST_PAUSE_NO_DEBUGGER");
        DemoBreakpoints.TrySetArmed("TEST_PAUSE_NO_DEBUGGER", armed: true);

        // Test hosts run without an attached debugger, so Pause must not consume or break.
        DemoBreakpoints.Pause("TEST_PAUSE_NO_DEBUGGER");

        Assert.Contains(
            DemoBreakpoints.GetStatus(),
            snippet => snippet is { SnippetName: "TEST_PAUSE_NO_DEBUGGER", IsArmed: true });
    }
}
