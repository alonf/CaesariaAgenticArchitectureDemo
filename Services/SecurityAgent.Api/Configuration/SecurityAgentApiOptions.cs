using System.ComponentModel.DataAnnotations;

namespace SecurityAgent.Api.Configuration;

/// <summary>
/// Configures the Security Operations Agent: the only service permitted to read the Security Hub.
/// </summary>
internal sealed class SecurityAgentApiOptions : IValidatableObject
{
    internal const string SectionName = "SecurityAgentApi";

    /// <summary>
    /// Gets or sets the base address of the authoritative Security Hub.
    /// </summary>
    [Required]
    public string SecurityHubBaseUri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Microsoft Foundry project endpoint used to run the Security Agent.
    /// </summary>
    [Required]
    public string FoundryProjectEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Microsoft Foundry model deployment name used by the Security Agent.
    /// </summary>
    [Required]
    public string ModelDeploymentName { get; set; } = "gpt-5.5";

    /// <summary>
    /// Gets or sets the projector-friendly Security Agent identity name.
    /// </summary>
    [Required]
    public string AgentName { get; set; } = "Caesarea Security Operations Agent";

    /// <summary>
    /// Gets or sets the wall-clock timeout for one assessment.
    /// </summary>
    [Range(5, 120)]
    public int RequestTimeoutSeconds { get; set; } = 45;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!ServiceUriValidator.TryValidateAbsoluteOrServiceDiscoveryUri(SecurityHubBaseUri, out _))
        {
            yield return new ValidationResult(
                "SecurityAgentApi:SecurityHubBaseUri must be a valid absolute or Aspire service-discovery URI.",
                [nameof(SecurityHubBaseUri)]);
        }

        if (!Uri.TryCreate(FoundryProjectEndpoint, UriKind.Absolute, out _))
        {
            yield return new ValidationResult(
                "SecurityAgentApi:FoundryProjectEndpoint must be an absolute URI.",
                [nameof(FoundryProjectEndpoint)]);
        }
    }
}
