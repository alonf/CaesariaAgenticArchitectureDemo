using System.ComponentModel.DataAnnotations;

namespace EnergyHub.Api.Configuration;

internal sealed class EnergyHubApiOptions : IValidatableObject
{
    internal const string SectionName = "EnergyHubApi";

    [Required]
    public string SmartPoleBaseUri { get; set; } = string.Empty;

    /// <summary>
    /// Whether to map the presenter-facing surface: the admin reset and scenario endpoints, the MCP
    /// server, and the demo breakpoints.
    /// </summary>
    /// <remarks>
    /// True locally, where the whole point is that a presenter can drive the scenario. False in
    /// Azure, where the Energy Hub has public ingress so that a hosted agent outside its VNet can
    /// read from it. That ingress authenticates callers, but authentication is not authorization:
    /// Container Apps checks that a token is valid and meant for this service, and does not check
    /// which application role it carries. Anything holding such a token would otherwise reach
    /// admin/reset - and resetting the scenario mid-lecture is the one failure that cannot be
    /// recovered gracefully.
    /// </remarks>
    public bool EnableDemoControlSurface { get; set; } = true;

    [Range(1, 100)]
    public int DefaultRecentActivityLimit { get; set; } = 20;

    [Range(1, 200)]
    public int MaxActivityLimit { get; set; } = 50;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!ServiceUriValidator.TryValidateAbsoluteOrServiceDiscoveryUri(SmartPoleBaseUri, out _))
        {
            yield return new ValidationResult(
                "EnergyHubApi:SmartPoleBaseUri must be a valid absolute or Aspire service-discovery URI.",
                [nameof(SmartPoleBaseUri)]);
        }

        if (DefaultRecentActivityLimit > MaxActivityLimit)
        {
            yield return new ValidationResult(
                "DefaultRecentActivityLimit cannot exceed MaxActivityLimit.",
                [nameof(DefaultRecentActivityLimit), nameof(MaxActivityLimit)]);
        }
    }
}
