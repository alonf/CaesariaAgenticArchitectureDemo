using System.Text.RegularExpressions;

namespace Caesarea.Deterministic.Tests;

/// <summary>
/// Visual Studio is an optional debugger frontend, never part of the demo runtime. These guards
/// hold that line in both directions: nothing portable may grow a Windows target framework or a
/// Visual Studio dependency, and the helper that has both must stay outside the portable build.
/// </summary>
public sealed class DebuggerBoundaryTests
{
    private static readonly string[] PortableRoots = ["Apps", "Services", "Shared", "Contracts", "Caesarea.AppHost", "Caesarea.ServiceDefaults", "Tests"];
    private static readonly string[] VisualStudioTokens = ["Microsoft.VisualStudio.", "EnvDTE", "envdte"];

    [Fact]
    public void EveryPortableProjectTargetsPlainNet10()
    {
        var repositoryRoot = FindRepositoryRoot();

        foreach (var projectPath in PortableProjects(repositoryRoot))
        {
            var projectText = File.ReadAllText(projectPath);
            var frameworks = Regex.Matches(projectText, @"<TargetFrameworks?>([^<]+)</TargetFrameworks?>")
                .Select(match => match.Groups[1].Value)
                .ToList();

            Assert.True(frameworks.Count > 0, $"{projectPath} declares no target framework.");
            Assert.All(frameworks, framework => Assert.Equal("net10.0", framework));
        }
    }

    [Fact]
    public void NoPortableProjectReferencesVisualStudio()
    {
        var repositoryRoot = FindRepositoryRoot();

        foreach (var projectPath in PortableProjects(repositoryRoot))
        {
            var projectText = File.ReadAllText(projectPath);

            foreach (var token in VisualStudioTokens)
            {
                Assert.DoesNotContain(token, projectText, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void TheSwitchboardTalksToVisualStudioOnlyThroughTheHelperProcess()
    {
        // DemoControl stays portable by never touching COM or the DTE itself; the helper does.
        var repositoryRoot = FindRepositoryRoot();
        var sourceText = ReadSources(Path.Combine(repositoryRoot, "Apps", "DemoControl.Web"));

        Assert.DoesNotContain("EnvDTE", sourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Runtime.InteropServices.ComTypes", sourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("DllImport", sourceText, StringComparison.Ordinal);
    }

    [Fact]
    public void TheHelperStaysOutsideThePortableSolution()
    {
        var repositoryRoot = FindRepositoryRoot();
        var solutionText = File.ReadAllText(Path.Combine(repositoryRoot, "Caesarea.slnx"));
        var helperProject = Path.Combine(repositoryRoot, "tools", "visualstudio-demo-attach", "src", "VisualStudioDemoAttach.csproj");

        Assert.DoesNotContain("visualstudio-demo-attach", solutionText, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(helperProject), "The Visual Studio helper project is missing.");
        Assert.Contains("<TargetFramework>net10.0-windows</TargetFramework>", File.ReadAllText(helperProject), StringComparison.Ordinal);
    }

    [Fact]
    public void ServicesPauseOnDebuggerIsAttachedAndNothingElse()
    {
        // Whether the debugger is VS Code or Visual Studio, and whether the OS is Windows, is not
        // the service's business: it pauses when a debugger is on it, full stop.
        var repositoryRoot = FindRepositoryRoot();
        var breakpointsText = File.ReadAllText(Path.Combine(repositoryRoot, "Caesarea.ServiceDefaults", "DemoBreakpoints.cs"));

        Assert.Contains("Debugger.IsAttached", breakpointsText, StringComparison.Ordinal);
        Assert.Contains("Debugger.Break()", breakpointsText, StringComparison.Ordinal);
        Assert.DoesNotContain("OperatingSystem.", breakpointsText, StringComparison.Ordinal);
        Assert.DoesNotContain("IsOSPlatform", breakpointsText, StringComparison.Ordinal);
        Assert.DoesNotContain("VisualStudio", breakpointsText, StringComparison.Ordinal);
        Assert.DoesNotContain("VsCode", breakpointsText, StringComparison.Ordinal);
    }

    private static IEnumerable<string> PortableProjects(string repositoryRoot) =>
        PortableRoots
            .Select(root => Path.Combine(repositoryRoot, root))
            .Where(Directory.Exists)
            .SelectMany(root => Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories))
            .Where(path => !IsBuildOutput(path));

    private static string ReadSources(string directory) =>
        string.Join(
            Environment.NewLine,
            Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(directory, "*.razor", SearchOption.AllDirectories))
                .Where(path => !IsBuildOutput(path))
                .Select(File.ReadAllText));

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
        || path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Caesarea.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root could not be located from the test output directory.");
    }
}
