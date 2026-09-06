using OperationsAgent.Api.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class ArchitectureBoundaryTests
{
    private static readonly string[] HostedProjectRoots = ["Services", "Apps"];

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

        // The agent's local function tools live in its capabilities, and there are exactly four:
        // one reads authoritative state, one asks the governed operation to start from the Workflow
        // stage, one files a work item behind the approval wrapper, and one looks an incident up.
        // None performs a write - the only direct write the agent ever holds is the MCP tool in its
        // window - and the spine registers no tool of its own.
        var agentText = File.ReadAllText(Path.Combine(repositoryRoot, "Services", "OperationsAgent.Api", "Services", "FoundryOperationsAgent.cs"));
        var capabilitiesText = ReadCapabilities(repositoryRoot);
        Assert.Equal(0, CountOccurrences(agentText, "AIFunctionFactory.Create("));
        Assert.Equal(4, CountOccurrences(capabilitiesText, "AIFunctionFactory.Create("));
        Assert.Contains("EnergyTools.StreetlightStateToolName", capabilitiesText, StringComparison.Ordinal);
        Assert.Contains("OperationsAgentToolNames.StartRestoreLightingOperation", capabilitiesText, StringComparison.Ordinal);
        // The maintenance capability only reaches the model wrapped for approval - pinned
        // separately by SensitiveWorkItemToolIsAlwaysApprovalWrapped.
        Assert.Contains("OperationsAgentToolNames.CreateMaintenanceWorkItem", capabilitiesText, StringComparison.Ordinal);
        // Its read-only partner, the incident lookup, is deliberately not wrapped: reading commits nothing.
        Assert.Contains("OperationsAgentToolNames.GetIncident", capabilitiesText, StringComparison.Ordinal);
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
    public void OperationsAgentHasNoPathToTheWorkforceHub()
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

        // The A2A claim rests on this. If this service could read the work-order system of record,
        // delegating to its agent would be theatre - and it would also be holding the commercial
        // and personal fields the whole stage exists to keep inside the workforce domain.
        Assert.DoesNotContain("workforcehub", sourceText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Workforce.Contracts", sourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/workforce", sourceText, StringComparison.OrdinalIgnoreCase);

        // What it does have is the peer agent's address, and only that.
        Assert.Contains("WorkforceAgentBaseUri", sourceText, StringComparison.Ordinal);
    }

    [Fact]
    public void SensitiveWorkItemToolIsAlwaysApprovalWrapped()
    {
        var repositoryRoot = FindRepositoryRoot();
        var capabilityText = ReadCapability(repositoryRoot, "ToolApprovalCapability.cs");

        // The work-item capability may only reach the model through the approval wrapper: an
        // unwrapped AIFunctionFactory.Create over the maintenance tool would hand the agent an
        // unsupervised way to commit city resources.
        var wrapperIndex = capabilityText.IndexOf("new ApprovalRequiredAIFunction(", StringComparison.Ordinal);
        var toolIndex = capabilityText.IndexOf("maintenanceTools.CreateMaintenanceWorkItem", StringComparison.Ordinal);

        Assert.True(wrapperIndex >= 0, "The maintenance tool is no longer wrapped for approval.");
        Assert.True(toolIndex > wrapperIndex, "The maintenance tool must be created inside the approval wrapper.");
        Assert.Equal(1, CountOccurrences(capabilityText, "maintenanceTools.CreateMaintenanceWorkItem"));
        Assert.Equal(1, CountOccurrences(ReadCapabilities(repositoryRoot), "maintenanceTools.CreateMaintenanceWorkItem"));
        // The capability admits itself only from the ToolApproval stage.
        Assert.Contains("stage >= DemoStage.ToolApproval", capabilityText, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteCapabilityIsStageGatedInTheAgent()
    {
        var repositoryRoot = FindRepositoryRoot();
        var streetlightText = ReadCapability(repositoryRoot, "StreetlightToolsCapability.cs");
        var workflowText = ReadCapability(repositoryRoot, "RemediationWorkflowCapability.cs");

        // The direct write exists in exactly one stage window: it appears at InteractiveInput,
        // where MRTR guards it, and is withdrawn at Workflow, where the agent must request the
        // governed operation instead of performing the write itself.
        var windowGateIndex = streetlightText.IndexOf(
            "currentStage >= DemoStage.InteractiveInput && currentStage < DemoStage.Workflow", StringComparison.Ordinal);
        var restoreToolIndex = streetlightText.IndexOf("OperationsAgentToolNames.RestoreScheduledMode", StringComparison.Ordinal);
        var workflowGateIndex = workflowText.IndexOf("stage >= DemoStage.Workflow", StringComparison.Ordinal);
        var workflowToolIndex = workflowText.IndexOf("OperationsAgentToolNames.StartRestoreLightingOperation", StringComparison.Ordinal);

        Assert.True(windowGateIndex >= 0, "The InteractiveInput..Workflow stage window for the direct write is missing.");
        Assert.True(restoreToolIndex > windowGateIndex, "The restore tool must be exposed only inside the stage window.");
        Assert.True(workflowGateIndex >= 0, "The Workflow stage gate for the governed operation is missing.");
        Assert.True(workflowToolIndex > workflowGateIndex, "The workflow-start tool must be exposed only behind the Workflow stage gate.");
        // Counted on the registration itself, not on every mention of the name: the approval
        // prompt also names the capability, and that is metadata, not a second exposure.
        Assert.Equal(1, CountOccurrences(ReadCapabilities(repositoryRoot), "FindDiscoveredTool(discoveredTools, OperationsAgentToolNames.RestoreScheduledMode, EnergyHubSourceName)"));
    }

    [Fact]
    public void EveryHostedProjectDeclaresItsOwnEndpoint()
    {
        // Aspire takes each project's endpoints from its launch profile. A project without one gets
        // no endpoint at all: service discovery cannot resolve it, and Kestrel falls back to the
        // default port - so the second such project fails to bind and the process exits. That is
        // how the Security Agent died under the AppHost while every scratchpad run passed, because
        // a hand-rolled stack assigns ports explicitly and never exercises this.
        var repositoryRoot = FindRepositoryRoot();
        Dictionary<string, string> urlOwners = new(StringComparer.OrdinalIgnoreCase);

        foreach (var projectDirectory in HostedProjectRoots
            .Select(root => Path.Combine(repositoryRoot, root))
            .Where(Directory.Exists)
            .SelectMany(Directory.GetDirectories))
        {
            var projectName = Path.GetFileName(projectDirectory);
            var launchSettingsPath = Path.Combine(projectDirectory, "Properties", "launchSettings.json");

            // Not every project is composed by the AppHost. OperationsAgent.Hosted is a container
            // the Foundry platform runs, so nothing here allocates its port - but it still declares
            // one, because a local `dotnet run` and the platform have to agree on where it listens.
            // The uniqueness check below applies to it either way.
            Assert.True(
                File.Exists(launchSettingsPath),
                $"{projectName} declares no port. Aspire-composed projects then get no endpoint and fall back to the default Kestrel port; platform-hosted ones become impossible to run locally in the same way they run deployed.");

            // A project lists the same URL in both its http and https profiles, so the comparison
            // is between projects, not within one.
            var applicationUrls = System.Text.RegularExpressions.Regex
                .Matches(File.ReadAllText(launchSettingsPath), @"https?://localhost:(\d+)")
                .Select(match => match.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var url in applicationUrls)
            {
                Assert.False(
                    urlOwners.TryGetValue(url, out var owner),
                    $"{projectName} and {owner} both bind {url}; the second to start cannot bind and exits.");

                urlOwners[url] = projectName;
            }
        }
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

    // The agent's capabilities, one file per stage, are where its tools are registered.
    private static string CapabilitiesDirectory(string repositoryRoot) =>
        Path.Combine(repositoryRoot, "Services", "OperationsAgent.Api", "Services", "Capabilities");

    private static string ReadCapability(string repositoryRoot, string fileName) =>
        File.ReadAllText(Path.Combine(CapabilitiesDirectory(repositoryRoot), fileName));

    private static string ReadCapabilities(string repositoryRoot) =>
        string.Join(Environment.NewLine, Directory.GetFiles(CapabilitiesDirectory(repositoryRoot), "*.cs").Order(StringComparer.Ordinal).Select(File.ReadAllText));

    private static int CountOccurrences(string value, string searchText) =>
        (value.Length - value.Replace(searchText, string.Empty, StringComparison.Ordinal).Length) / searchText.Length;
}
