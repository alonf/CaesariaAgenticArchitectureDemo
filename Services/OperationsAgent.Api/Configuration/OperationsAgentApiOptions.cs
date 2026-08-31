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
    /// Gets or sets the base address of the Command Center that owns the authoritative demo stage.
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
    /// Gets or sets the Microsoft Foundry model deployment name used by the Operations Agent.
    /// </summary>
    [Required]
    public string ModelDeploymentName { get; set; } = "gpt-5.5";

    /// <summary>
    /// Gets or sets the projector-friendly Operations Agent identity name.
    /// </summary>
    [Required]
    public string AgentName { get; set; } = "Caesarea Operations Agent";

    /// <summary>
    /// Gets or sets the maximum model/function round trips allowed for one request.
    /// </summary>
    [Range(2, 12)]
    public int MaxFunctionIterations { get; set; } = 7;

    /// <summary>
    /// Gets or sets the wall-clock timeout for one agent request.
    /// </summary>
    [Range(10, 180)]
    public int RequestTimeoutSeconds { get; set; } = 90;

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
