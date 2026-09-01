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
        var toolsetPath = Path.Combine(repositoryRoot, "Services", "OperationsAgent.Api", "Services", "EnergyTools.cs");
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

        Assert.Equal("get_streetlight_state", EnergyTools.StreetlightStateToolName);
        Assert.StartsWith("get_", EnergyTools.StreetlightStateToolName, StringComparison.Ordinal);
        Assert.DoesNotContain("Diagnosis", toolsetText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Incident", toolsetText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Activity", toolsetText, StringComparison.OrdinalIgnoreCase);

        // The agent composes exactly two local function tools. One reads authoritative state and
        // one asks the governed operation to start from the Workflow stage. Neither performs a
        // write, and the only direct write the agent ever holds is the MCP tool in its window.
        var agentPath = Path.Combine(repositoryRoot, "Services", "OperationsAgent.Api", "Services", "FoundryOperationsAgent.cs");
        var agentText = File.ReadAllText(agentPath);
        Assert.Equal(3, CountOccurrences(agentText, "AIFunctionFactory.Create("));
        Assert.Contains("EnergyTools.StreetlightStateToolName", agentText, StringComparison.Ordinal);
        Assert.Contains("OperationsAgentToolNames.StartRestoreLightingOperation", agentText, StringComparison.Ordinal);
        // The third is the maintenance capability, and it only reaches the model wrapped for
        // approval - pinned separately by SensitiveWorkItemToolIsAlwaysApprovalWrapped.
        Assert.Contains("OperationsAgentToolNames.CreateMaintenanceWorkItem", agentText, StringComparison.Ordinal);
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

    [Fact]
    public void RestoreToolPausesForApprovalBeforeAnySideEffect()
    {
        var repositoryRoot = FindRepositoryRoot();
        var toolText = File.ReadAllText(Path.Combine(repositoryRoot, "Services", "EnergyHub.Api", "Services", "EnergyMcpTools.cs"));

        // The write tool's no-side-effect-before-input guard: the one-time request-state check
        // and the confirmation check both come before the restore call, MRTR support is verified,
        // and the pause is a protocol-level InputRequiredException - not UI convention.
        var stateCheckIndex = toolText.IndexOf("requestStateStore.TryConsume(", StringComparison.Ordinal);
        var confirmationCheckIndex = toolText.IndexOf("IsApproved(response)", StringComparison.Ordinal);
        var restoreCallIndex = toolText.IndexOf("RestoreScheduledModeAsync(assetId", StringComparison.Ordinal);

        Assert.True(stateCheckIndex >= 0, "The restore tool no longer validates the one-time request state.");
        Assert.True(confirmationCheckIndex > stateCheckIndex, "The confirmation check must come after the request-state validation.");
        Assert.True(restoreCallIndex > confirmationCheckIndex, "The restore call must be reachable only after the confirmation check.");
        Assert.Contains("IsMrtrSupported", toolText, StringComparison.Ordinal);
        Assert.Contains("InputRequiredException", toolText, StringComparison.Ordinal);
    }

    [Fact]
    public void OperationsAgentHasNoPathToTheSecurityHub()
    {
        var repositoryRoot = FindRepositoryRoot();
        var operationsAgentRoot = Path.Combine(repositoryRoot, "Services", "OperationsAgent.Api");
        var sourceText = string.Join(
            Environment.NewLine,
            Directory.GetFiles(operationsAgentRoot, "*.*", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));

        // This is the assertion the second agent's justification rests on. If the Operations Agent
        // could read the Security Hub directly, the right answer would be a tool, not an agent
        // (slide 36) - so the hub must be unreachable from here, by address or by contract.
        Assert.DoesNotContain("securityhub", sourceText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Security.Contracts", sourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/security", sourceText, StringComparison.OrdinalIgnoreCase);

        // What it does have is the consult across the boundary, and only that.
        Assert.Contains("securityagent-mcp", sourceText, StringComparison.Ordinal);
    }

    [Fact]
    public void SensitiveWorkItemToolIsAlwaysApprovalWrapped()
    {
        var repositoryRoot = FindRepositoryRoot();
        var agentText = File.ReadAllText(Path.Combine(repositoryRoot, "Services", "OperationsAgent.Api", "Services", "FoundryOperationsAgent.cs"));

        // The work-item capability may only reach the model through the approval wrapper: an
        // unwrapped AIFunctionFactory.Create over the maintenance tool would hand the agent an
        // unsupervised way to commit city resources.
        var wrapperIndex = agentText.IndexOf("new ApprovalRequiredAIFunction(", StringComparison.Ordinal);
        var toolIndex = agentText.IndexOf("maintenanceTools.CreateMaintenanceWorkItemAsync", StringComparison.Ordinal);

        Assert.True(wrapperIndex >= 0, "The maintenance tool is no longer wrapped for approval.");
        Assert.True(toolIndex > wrapperIndex, "The maintenance tool must be created inside the approval wrapper.");
        Assert.Equal(1, CountOccurrences(agentText, "maintenanceTools.CreateMaintenanceWorkItemAsync"));
        Assert.Contains("currentStage >= DemoStage.ToolApproval", agentText, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteCapabilityIsStageGatedInTheAgent()
    {
        var repositoryRoot = FindRepositoryRoot();
        var agentText = File.ReadAllText(Path.Combine(repositoryRoot, "Services", "OperationsAgent.Api", "Services", "FoundryOperationsAgent.cs"));

        // The direct write exists in exactly one stage window: it appears at InteractiveInput,
        // where MRTR guards it, and is withdrawn at Workflow, where the agent must request the
        // governed operation instead of performing the write itself.
        var windowGateIndex = agentText.IndexOf(
            "currentStage >= DemoStage.InteractiveInput && currentStage < DemoStage.Workflow", StringComparison.Ordinal);
        var restoreToolIndex = agentText.IndexOf("OperationsAgentToolNames.RestoreScheduledMode", StringComparison.Ordinal);
        var workflowToolIndex = agentText.IndexOf("OperationsAgentToolNames.StartRestoreLightingOperation", StringComparison.Ordinal);
        var workflowGateIndex = agentText.IndexOf("currentStage >= DemoStage.Workflow", StringComparison.Ordinal);

        Assert.True(windowGateIndex >= 0, "The InteractiveInput..Workflow stage window for the direct write is missing.");
        Assert.True(restoreToolIndex > windowGateIndex, "The restore tool must be exposed only inside the stage window.");
        Assert.True(workflowGateIndex >= 0, "The Workflow stage gate for the governed operation is missing.");
        Assert.True(workflowToolIndex > workflowGateIndex, "The workflow-start tool must be exposed only behind the Workflow stage gate.");
        // Counted on the registration itself, not on every mention of the name: the approval
        // prompt also names the capability, and that is metadata, not a second exposure.
        Assert.Equal(1, CountOccurrences(agentText, "FindDiscoveredTool(discoveredTools, OperationsAgentToolNames.RestoreScheduledMode)"));
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

    private static int CountOccurrences(string value, string searchText) =>
        (value.Length - value.Replace(searchText, string.Empty, StringComparison.Ordinal).Length) / searchText.Length;
}
