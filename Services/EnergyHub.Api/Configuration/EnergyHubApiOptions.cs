using System.ComponentModel.DataAnnotations;

namespace EnergyHub.Api.Configuration;

internal sealed class EnergyHubApiOptions : IValidatableObject
{
    internal const string SectionName = "EnergyHubApi";

    [Required]
    public string SmartPoleBaseUri { get; set; } = string.Empty;

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
