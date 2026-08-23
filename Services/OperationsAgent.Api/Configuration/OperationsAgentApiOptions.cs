using System.ComponentModel.DataAnnotations;

namespace OperationsAgent.Api.Configuration;

/// <summary>
/// Configures the read-only Operations Agent API, including its deterministic read boundaries and the
/// Microsoft Foundry project used for model inference.
/// </summary>
internal sealed class OperationsAgentApiOptions : IValidatableObject
{
    internal const string SectionName = "OperationsAgentApi";

    /// <summary>
    /// Gets or sets the base address of the authoritative Energy Hub used for read-only evidence.
    /// </summary>
    [Required]
    public string EnergyHubBaseUri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base address of the Command Center used for read-only evidence.
    /// </summary>
    [Required]
    public string CommandCenterBaseUri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Microsoft Foundry project endpoint used to run the Operations Agent.
    /// This value is a non-secret development default and does not require any key or secret.
    /// </summary>
    [Required]
    public string FoundryProjectEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Microsoft Foundry model deployment name used for investigation reasoning.
    /// </summary>
    [Required]
    public string ModelDeploymentName { get; set; } = "gpt-5.2-chat";

    /// <summary>
    /// Gets or sets the projector-friendly Operations Agent identity name.
    /// </summary>
    [Required]
    public string AgentName { get; set; } = "Operations Agent";

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!ServiceUriValidator.TryValidateAbsoluteOrServiceDiscoveryUri(EnergyHubBaseUri, out _))
        {
            yield return new ValidationResult(
                "OperationsAgentApi:EnergyHubBaseUri must be a valid absolute or Aspire service-discovery URI.",
                [nameof(EnergyHubBaseUri)]);
        }

        if (!ServiceUriValidator.TryValidateAbsoluteOrServiceDiscoveryUri(CommandCenterBaseUri, out _))
        {
            yield return new ValidationResult(
                "OperationsAgentApi:CommandCenterBaseUri must be a valid absolute or Aspire service-discovery URI.",
                [nameof(CommandCenterBaseUri)]);
        }

        if (!Uri.TryCreate(FoundryProjectEndpoint, UriKind.Absolute, out var foundryUri) || foundryUri.Scheme != Uri.UriSchemeHttps)
        {
            yield return new ValidationResult(
                "OperationsAgentApi:FoundryProjectEndpoint must be an absolute https URI.",
                [nameof(FoundryProjectEndpoint)]);
        }
    }
}
