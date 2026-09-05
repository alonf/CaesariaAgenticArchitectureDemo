using System.ComponentModel.DataAnnotations;
using CommandCenter.Api.Configuration;
using CommandCenter.Web.Configuration;
using DemoScenario.Api.Configuration;
using OperationsAgent.Api.Configuration;

namespace Caesarea.Deterministic.Tests;

public sealed class ValidationAndOptionsTests
{
    [Fact]
    public void EnergyScenarioSyncRequestRequiresSummary()
    {
        var request = new EnergyScenarioSyncRequest(false, null, string.Empty);

        var results = Validate(request);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(EnergyScenarioSyncRequest.Summary), StringComparer.Ordinal));
    }

    [Fact]
    public void SmartPoleBehaviorConfigurationRejectsNegativeCommandDelay()
    {
        var configuration = new SmartPoleBehaviorConfiguration(-1, false, false);

        var results = Validate(configuration);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(SmartPoleBehaviorConfiguration.CommandDelayMs), StringComparer.Ordinal));
    }

    [Fact]
    public void CommandCenterApiOptionsRejectInvalidServiceUri()
    {
        var options = new CommandCenterApiOptions
        {
            EnergyHubBaseUri = "not-a-uri",
            DefaultSnapshotActivityLimit = 12,
            DefaultRecentActivityLimit = 20,
            MaxActivityLimit = 50
        };

        var results = Validate(options);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(CommandCenterApiOptions.EnergyHubBaseUri), StringComparer.Ordinal));
    }

    [Fact]
    public void DemoScenarioApiOptionsAcceptAspireServiceDiscoveryUris()
    {
        var options = new DemoScenarioApiOptions
        {
            SmartPoleBaseUri = "https+http://smartpole-simulator-api",
            EnergyHubBaseUri = "https+http://energyhub-api",
            CommandCenterBaseUri = "https+http://commandcenter-api",
            SecurityHubBaseUri = "https+http://securityhub-api",
            WorkforceHubBaseUri = "https+http://workforcehub-api",
            OperationsAgentBaseUri = "https+http://operationsagent-api"
        };

        var results = Validate(options);

        Assert.Empty(results);
    }

    [Fact]
    public void HostedAgentEndpointOnTheFoundrySuffixIsAccepted()
    {
        var options = CreateCommandCenterWebOptions();
        options.HostedAgent.ProjectEndpoint = "https://aif-example.services.ai.azure.com/api/projects/caesarea-dev";

        Assert.Empty(Validate(options));
    }

    [Fact]
    public void HostedAgentEndpointOnAForeignHostIsRejected()
    {
        // The presenter's bearer token is attached to this endpoint, so a typo'd or tampered host
        // must fail at startup rather than receive the token.
        var options = CreateCommandCenterWebOptions();
        options.HostedAgent.ProjectEndpoint = "https://evil.example.com/api/projects/caesarea-dev";

        var results = Validate(options);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(CommandCenterWebOptions.HostedAgent), StringComparer.Ordinal));
    }

    [Fact]
    public void HostedAgentEndpointOnAnExplicitlyAllowedHostIsAccepted()
    {
        // Private-endpoint setups opt in host-by-host; a suffix pattern would be a hole, an exact
        // name is a decision.
        var options = CreateCommandCenterWebOptions();
        options.HostedAgent.ProjectEndpoint = "https://foundry.corp.internal/api/projects/caesarea-dev";
        options.HostedAgent.AllowedEndpointHosts.Add("foundry.corp.internal");

        Assert.Empty(Validate(options));
    }

    [Theory]
    [InlineData("https://aif-example.services.ai.azure.com/api/projects/caesarea-dev?sneaky=1")]
    [InlineData("https://user@aif-example.services.ai.azure.com/api/projects/caesarea-dev")]
    [InlineData("https://aif-example.services.ai.azure.com/api/projects/caesarea-dev#fragment")]
    [InlineData("https://aif-example.services.ai.azure.com/somewhere/else")]
    public void HostedAgentEndpointDecorationsAndWrongPathsAreRejected(string endpoint)
    {
        var options = CreateCommandCenterWebOptions();
        options.HostedAgent.ProjectEndpoint = endpoint;

        var results = Validate(options);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(CommandCenterWebOptions.HostedAgent), StringComparer.Ordinal));
    }

    private static CommandCenterWebOptions CreateCommandCenterWebOptions() => new()
    {
        BaseUri = "https+http://commandcenter-api",
        OperationsAgentBaseUri = "https+http://operationsagent-api"
    };

    [Fact]
    public void ServiceUriValidatorRejectsUnsupportedSchemes()
    {
        var isValid = ServiceUriValidator.TryValidateAbsoluteOrServiceDiscoveryUri("ftp://example.com", out var uri);

        Assert.False(isValid);
        Assert.Null(uri);
    }

    [Fact]
    public void SetLampStateCommandRequiresDesiredStatePresence()
    {
        var command = new SetLampStateCommand(DemoAssets.StreetlightAssetId, null);

        var results = Validate(command);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(SetLampStateCommand.DesiredIsOn), StringComparer.Ordinal));
    }

    [Fact]
    public void OperationsAgentApiOptionsAcceptValidConfiguration()
    {
        var options = new OperationsAgentApiOptions
        {
            EnergyHubBaseUri = "https+http://energyhub-api",
            CommandCenterBaseUri = "https+http://commandcenter-api",
            SecurityAgentBaseUri = "https+http://securityagent-api",
            WorkforceAgentBaseUri = "https+http://workforceagent-api",
            FoundryProjectEndpoint = "https://alonlecturedemo-resource.services.ai.azure.com/api/projects/alonlecturedemo",
            ModelDeploymentName = "gpt-5.5",
            AgentName = "Caesarea Operations Agent"
        };

        var results = Validate(options);

        Assert.Empty(results);
    }

    [Fact]
    public void OperationsAgentApiOptionsRejectsUnboundedExecutionSettings()
    {
        var options = new OperationsAgentApiOptions
        {
            EnergyHubBaseUri = "https+http://energyhub-api",
            FoundryProjectEndpoint = "https://alonlecturedemo-resource.services.ai.azure.com/api/projects/alonlecturedemo",
            ModelDeploymentName = "gpt-5.5",
            AgentName = "Caesarea Operations Agent",
            MaxFunctionIterations = 40,
            RequestTimeoutSeconds = 600
        };

        var results = Validate(options);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(OperationsAgentApiOptions.MaxFunctionIterations), StringComparer.Ordinal));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(OperationsAgentApiOptions.RequestTimeoutSeconds), StringComparer.Ordinal));
    }

    [Fact]
    public void OperationsAgentApiOptionsRejectNonHttpsFoundryEndpoint()
    {
        var options = new OperationsAgentApiOptions
        {
            EnergyHubBaseUri = "https+http://energyhub-api",
            CommandCenterBaseUri = "https+http://commandcenter-api",
            SecurityAgentBaseUri = "https+http://securityagent-api",
            WorkforceAgentBaseUri = "https+http://workforceagent-api",
            FoundryProjectEndpoint = "http://insecure-endpoint.example.com/api/projects/demo",
            ModelDeploymentName = "gpt-5.5",
            AgentName = "Caesarea Operations Agent"
        };

        var results = Validate(options);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(OperationsAgentApiOptions.FoundryProjectEndpoint), StringComparer.Ordinal));
    }

    [Fact]
    public void OperationsAgentApiOptionsRejectInvalidServiceUris()
    {
        var options = new OperationsAgentApiOptions
        {
            EnergyHubBaseUri = "not-a-uri",
            CommandCenterBaseUri = "https+http://commandcenter-api",
            SecurityAgentBaseUri = "https+http://securityagent-api",
            WorkforceAgentBaseUri = "https+http://workforceagent-api",
            FoundryProjectEndpoint = "https://alonlecturedemo-resource.services.ai.azure.com/api/projects/alonlecturedemo",
            ModelDeploymentName = "gpt-5.5",
            AgentName = "Caesarea Operations Agent"
        };

        var results = Validate(options);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(OperationsAgentApiOptions.EnergyHubBaseUri), StringComparer.Ordinal));
    }

    private static List<ValidationResult> Validate(object instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        List<ValidationResult> validationResults = [];
        Validator.TryValidateObject(instance, new ValidationContext(instance), validationResults, validateAllProperties: true);
        return validationResults;
    }
}
