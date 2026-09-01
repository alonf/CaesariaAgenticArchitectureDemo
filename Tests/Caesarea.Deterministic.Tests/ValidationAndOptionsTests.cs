using System.ComponentModel.DataAnnotations;
using CommandCenter.Api.Configuration;
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
            OperationsAgentBaseUri = "https+http://operationsagent-api"
        };

        var results = Validate(options);

        Assert.Empty(results);
    }

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
