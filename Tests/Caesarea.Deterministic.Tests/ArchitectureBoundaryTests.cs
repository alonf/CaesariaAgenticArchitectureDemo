using OperationsAgent.Api.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class ArchitectureBoundaryTests
{
    [Fact]
    public void CommandCenterProjectDoesNotReferenceSmartPoleSimulator()
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

    [Fact]
    public void OperationsAgentProjectDoesNotReferenceSmartPoleSimulator()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(repositoryRoot, "Services", "OperationsAgent.Api", "OperationsAgent.Api.csproj");
        var sourceDirectory = Path.Combine(repositoryRoot, "Services", "OperationsAgent.Api");

        var projectText = File.ReadAllText(projectPath);
        Assert.DoesNotContain("SmartPole.Simulator.Api.csproj", projectText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SmartPole.Contracts", projectText, StringComparison.OrdinalIgnoreCase);

        var sourceText = string.Join(
            Environment.NewLine,
            Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.DoesNotContain("SmartPole", sourceText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OperationsAgentExposesOnlyReadOnlyTools()
    {
        var repositoryRoot = FindRepositoryRoot();
        var toolsetPath = Path.Combine(repositoryRoot, "Services", "OperationsAgent.Api", "Services", "OperationsToolset.cs");
        var toolsetText = File.ReadAllText(toolsetPath);

        string[] forbiddenTokens =
        [
            "RestoreScheduledMode",
            "ApplyScenario",
            "SetLampState",
            "Reset(",
            "ResetAsync",
            "Write",
            "Command(",
            "CommandAsync"
        ];

        foreach (var token in forbiddenTokens)
        {
            Assert.DoesNotContain(token, toolsetText, StringComparison.Ordinal);
        }

        var toolMethodNames = new[]
        {
            OperationsToolset.CustomerReportToolName,
            OperationsToolset.EnergyAssetStateToolName,
            OperationsToolset.EnergyRecentActivityToolName,
            OperationsToolset.IncidentContextToolName
        };

        Assert.All(toolMethodNames, name => Assert.StartsWith("get_", name, StringComparison.Ordinal));
    }

    [Fact]
    public void OperationsAgentProjectExposesNoCommandOrWriteEndpoints()
    {
        var repositoryRoot = FindRepositoryRoot();
        var programPath = Path.Combine(repositoryRoot, "Services", "OperationsAgent.Api", "Program.cs");
        var programText = File.ReadAllText(programPath);

        Assert.DoesNotContain("restore-scheduled-mode", programText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/admin/", programText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MapPut", programText, StringComparison.Ordinal);
        Assert.DoesNotContain("MapDelete", programText, StringComparison.Ordinal);
    }

    [Fact]
    public void ServiceDefaultsDoesNotReferenceDomainContracts()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(repositoryRoot, "Caesarea.ServiceDefaults", "Caesarea.ServiceDefaults.csproj");
        var sourceDirectory = Path.Combine(repositoryRoot, "Caesarea.ServiceDefaults");

        var projectText = File.ReadAllText(projectPath);
        Assert.DoesNotContain("ProjectReference", projectText, StringComparison.OrdinalIgnoreCase);

        var sourceText = string.Join(
            Environment.NewLine,
            Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.DoesNotContain(".Contracts", sourceText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CanonicalModel", sourceText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MiscellaneousSharedContractsProjectDoesNotExist()
    {
        var repositoryRoot = FindRepositoryRoot();

        Assert.False(Directory.Exists(Path.Combine(repositoryRoot, "Shared", "Contracts")));
        Assert.False(File.Exists(Path.Combine(repositoryRoot, "Shared", "Contracts", "Caesarea.Contracts.csproj")));
    }

    [Fact]
    public void LegacyContractBucketReferencesDoNotExist()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceText = string.Join(
            Environment.NewLine,
            Directory.GetFiles(repositoryRoot, "*.*", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Tests{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));

        Assert.DoesNotContain("Caesarea.Contracts", sourceText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Shared.Contracts", sourceText, StringComparison.OrdinalIgnoreCase);
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
