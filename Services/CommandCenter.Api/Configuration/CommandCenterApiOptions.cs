using System.ComponentModel.DataAnnotations;

namespace CommandCenter.Api.Configuration;

internal sealed class CommandCenterApiOptions : IValidatableObject
{
    internal const string SectionName = "CommandCenterApi";

    [Required]
    public string EnergyHubBaseUri { get; set; } = string.Empty;

    [Range(1, 100)]
    public int DefaultSnapshotActivityLimit { get; set; } = 12;

    [Range(1, 100)]
    public int DefaultRecentActivityLimit { get; set; } = 20;

    [Range(1, 200)]
    public int MaxActivityLimit { get; set; } = 50;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!ServiceUriValidator.TryValidateAbsoluteOrServiceDiscoveryUri(EnergyHubBaseUri, out _))
        {
            yield return new ValidationResult(
                "CommandCenterApi:EnergyHubBaseUri must be a valid absolute or Aspire service-discovery URI.",
                [nameof(EnergyHubBaseUri)]);
        }

        if (DefaultSnapshotActivityLimit > MaxActivityLimit)
        {
            yield return new ValidationResult(
                "DefaultSnapshotActivityLimit cannot exceed MaxActivityLimit.",
                [nameof(DefaultSnapshotActivityLimit), nameof(MaxActivityLimit)]);
        }

        if (DefaultRecentActivityLimit > MaxActivityLimit)
        {
            yield return new ValidationResult(
                "DefaultRecentActivityLimit cannot exceed MaxActivityLimit.",
                [nameof(DefaultRecentActivityLimit), nameof(MaxActivityLimit)]);
        }
    }
}
