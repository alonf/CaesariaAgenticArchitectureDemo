using System.ComponentModel.DataAnnotations;

namespace DemoScenario.Api.Configuration;

internal sealed class DemoScenarioApiOptions : IValidatableObject
{
    internal const string SectionName = "DemoScenarioApi";

    [Required]
    public string SmartPoleBaseUri { get; set; } = string.Empty;

    [Required]
    public string EnergyHubBaseUri { get; set; } = string.Empty;

    [Required]
    public string CommandCenterBaseUri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base address of the Security Hub, synchronized from the same scenario
    /// recipe so the Security and Energy domains agree once an application completes.
    /// </summary>
    [Required]
    public string SecurityHubBaseUri { get; set; } = string.Empty;

    [Required]
    public string OperationsAgentBaseUri { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!ServiceUriValidator.TryValidateAbsoluteOrServiceDiscoveryUri(SmartPoleBaseUri, out _))
        {
            yield return new ValidationResult(
                "DemoScenarioApi:SmartPoleBaseUri must be a valid absolute or Aspire service-discovery URI.",
                [nameof(SmartPoleBaseUri)]);
        }

        if (!ServiceUriValidator.TryValidateAbsoluteOrServiceDiscoveryUri(EnergyHubBaseUri, out _))
        {
            yield return new ValidationResult(
                "DemoScenarioApi:EnergyHubBaseUri must be a valid absolute or Aspire service-discovery URI.",
                [nameof(EnergyHubBaseUri)]);
        }

        if (!ServiceUriValidator.TryValidateAbsoluteOrServiceDiscoveryUri(CommandCenterBaseUri, out _))
        {
            yield return new ValidationResult(
                "DemoScenarioApi:CommandCenterBaseUri must be a valid absolute or Aspire service-discovery URI.",
                [nameof(CommandCenterBaseUri)]);
        }

        if (!ServiceUriValidator.TryValidateAbsoluteOrServiceDiscoveryUri(SecurityHubBaseUri, out _))
        {
            yield return new ValidationResult(
                "DemoScenarioApi:SecurityHubBaseUri must be a valid absolute or Aspire service-discovery URI.",
                [nameof(SecurityHubBaseUri)]);
        }

        if (!ServiceUriValidator.TryValidateAbsoluteOrServiceDiscoveryUri(OperationsAgentBaseUri, out _))
        {
            yield return new ValidationResult(
                "DemoScenarioApi:OperationsAgentBaseUri must be a valid absolute or Aspire service-discovery URI.",
                [nameof(OperationsAgentBaseUri)]);
        }
    }
}
