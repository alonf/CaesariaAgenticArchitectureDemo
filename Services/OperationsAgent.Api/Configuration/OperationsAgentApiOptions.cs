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
    /// Gets or sets the base address of the Security Operations Agent, consulted across the
    /// domain boundary at the MultiAgent stage. This service never addresses the Security Hub
    /// itself: the restricted records are reachable only through that agent's reasoning.
    /// </summary>
    [Required]
    public string SecurityAgentBaseUri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base address of the Caesarea Workforce Agent, consulted over A2A. This
    /// service has no address for the workforce system of record itself - the work orders are
    /// reachable only through that agent.
    /// </summary>
    [Required]
    public string WorkforceAgentBaseUri { get; set; } = string.Empty;

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

    /// <summary>
    /// Gets or sets the directory holding the agent's skills (SKILL.md folders). A relative value
    /// is resolved against the repository root so the presenter can live-edit the source files;
    /// when the directory cannot be located the Skills stage runs without skills.
    /// </summary>
    [Required]
    public string SkillsDirectory { get; set; } = "skills";

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

        if (!ServiceUriValidator.TryValidateAbsoluteOrServiceDiscoveryUri(SecurityAgentBaseUri, out _))
        {
            yield return new ValidationResult(
                "OperationsAgentApi:SecurityAgentBaseUri must be a valid absolute or Aspire service-discovery URI.",
                [nameof(SecurityAgentBaseUri)]);
        }

        if (!ServiceUriValidator.TryValidateAbsoluteOrServiceDiscoveryUri(WorkforceAgentBaseUri, out _))
        {
            yield return new ValidationResult(
                "OperationsAgentApi:WorkforceAgentBaseUri must be a valid absolute or Aspire service-discovery URI.",
                [nameof(WorkforceAgentBaseUri)]);
        }

        if (!Uri.TryCreate(FoundryProjectEndpoint, UriKind.Absolute, out var foundryUri) || foundryUri.Scheme != Uri.UriSchemeHttps)
        {
            yield return new ValidationResult(
                "OperationsAgentApi:FoundryProjectEndpoint must be an absolute https URI.",
                [nameof(FoundryProjectEndpoint)]);
        }
    }
}
