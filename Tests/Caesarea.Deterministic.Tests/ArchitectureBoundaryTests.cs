namespace Caesarea.Deterministic.Tests;

public sealed class ArchitectureBoundaryTests
{
    [Fact]
    public void CommandCenterProject_DoesNotReferenceSmartPoleSimulator()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(repositoryRoot, "Services", "CommandCenter.Api", "CommandCenter.Api.csproj");
        var sourceDirectory = Path.Combine(repositoryRoot, "Services", "CommandCenter.Api");

        var projectText = File.ReadAllText(projectPath);
        Assert.DoesNotContain("SmartPole.Simulator.Api.csproj", projectText, StringComparison.OrdinalIgnoreCase);

        var sourceText = string.Join(
            Environment.NewLine,
            Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.DoesNotContain("SmartPole.Simulator", sourceText, StringComparison.OrdinalIgnoreCase);
    }

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
