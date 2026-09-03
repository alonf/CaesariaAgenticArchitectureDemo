using System.ComponentModel.DataAnnotations;

namespace WorkforceAgent.Api.Configuration;

/// <summary>
/// Configures the Caesarea Workforce Agent: the only service permitted to read the Workforce Hub.
/// </summary>
internal sealed class WorkforceAgentApiOptions : IValidatableObject
{
    internal const string SectionName = "WorkforceAgentApi";

    /// <summary>
    /// Gets or sets the base address of the authoritative Workforce Hub.
    /// </summary>
    [Required]
    public string WorkforceHubBaseUri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Microsoft Foundry project endpoint used to run the agent.
    /// </summary>
    [Required]
    public string FoundryProjectEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Microsoft Foundry model deployment name used by the agent.
    /// </summary>
    [Required]
    public string ModelDeploymentName { get; set; } = "gpt-5.5";

    /// <summary>
    /// Gets or sets the projector-friendly agent identity name, published on the agent card.
    /// </summary>
    [Required]
    public string AgentName { get; set; } = "Caesarea Workforce Agent";

    /// <summary>
    /// Gets or sets the wall-clock timeout for one delegated task.
    /// </summary>
    [Range(5, 180)]
    public int RequestTimeoutSeconds { get; set; } = 60;

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!ServiceUriValidator.TryValidateAbsoluteOrServiceDiscoveryUri(WorkforceHubBaseUri, out _))
        {
            yield return new ValidationResult(
                "WorkforceAgentApi:WorkforceHubBaseUri must be a valid absolute or Aspire service-discovery URI.",
                [nameof(WorkforceHubBaseUri)]);
        }

        if (!Uri.TryCreate(FoundryProjectEndpoint, UriKind.Absolute, out var foundryUri) || foundryUri.Scheme != Uri.UriSchemeHttps)
        {
            yield return new ValidationResult(
                "WorkforceAgentApi:FoundryProjectEndpoint must be an absolute https URI.",
                [nameof(FoundryProjectEndpoint)]);
        }
    }
}
