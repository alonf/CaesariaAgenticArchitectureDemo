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
            ["AGENT_CREATION", "AGENT_SESSION", "AGENT_SKILLS", "CASE_MEMORY", "FUNCTION_TOOL", "KNOWLEDGE_RETRIEVAL"],
            snippetValues.OrderBy(value => value, StringComparer.Ordinal));

        foreach (var value in snippetValues)
        {
            // Which deck presents a concept is presentation metadata, not identity: identifiers
            // may not embed a lecture code (the retired H08_/W20_ prefixes) ...
            Assert.DoesNotMatch(@"^[A-Z]{1,4}\d+_", value);
            // ... nor a physical slide position (the retired _S13_ and SLIDE_13 styles).
            Assert.DoesNotMatch(@"(^|_)S\d+(_|$)", value);
            Assert.DoesNotMatch(@"SLIDE_?\d+", value);
        }
    }

    [Fact]
    public void SnippetIdentifiersMatchTheirSourceRegions()
    {
        // Demo snippet regions are the ALL_CAPS #region markers; ordinary organizational regions
        // use normal casing and are ignored here.
        var repositoryRoot = FindRepositoryRoot();
        var regionNames = Directory
            .GetFiles(Path.Combine(repositoryRoot, "Services"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), @"#region\s+([A-Z][A-Z0-9_]{2,})\s*$", RegexOptions.Multiline)
                .Select(match => match.Groups[1].Value.TrimEnd('\r')))
            .ToList();

        // Every snippet the registry names has exactly one exported source region, and every
        // exported region is registered - duplicates, orphans, and drift all fail here.
        Assert.Equal(
            GetDemoSnippetValues().OrderBy(value => value, StringComparer.Ordinal),
            regionNames.OrderBy(value => value, StringComparer.Ordinal));
    }

    [Fact]
    public void RegisteredBreakpointsCoverEveryDemoSnippet()
    {
        var programPath = Path.Combine(
            FindRepositoryRoot(), "Services", "OperationsAgent.Api", "Program.cs");
        var registration = Regex.Match(File.ReadAllText(programPath), @"MapDemoBreakpoints\(([^;]*)\);");

        Assert.True(registration.Success, "Program.cs no longer calls MapDemoBreakpoints.");

        // Runtime registration must name every DemoSnippets constant: a snippet that exists in the
        // registry and its source region but is missing here could never be armed from DemoControl.
        var snippetFieldNames = typeof(DemoSnippets)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => field.Name);

        foreach (var fieldName in snippetFieldNames)
        {
            Assert.Contains($"{nameof(DemoSnippets)}.{fieldName}", registration.Groups[1].Value, StringComparison.Ordinal);
        }
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
