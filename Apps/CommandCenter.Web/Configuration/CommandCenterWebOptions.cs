using System.ComponentModel.DataAnnotations;

namespace CommandCenter.Web.Configuration;

internal sealed class CommandCenterWebOptions : IValidatableObject
{
    internal const string SectionName = "CommandCenterWeb";

    [Required]
    public string BaseUri { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public string AssetId { get; set; } = DemoAssets.StreetlightAssetId;

    [Range(1, 100)]
    public int SnapshotActivityLimit { get; set; } = 16;

    [Range(1, 60)]
    public int PollingIntervalSeconds { get; set; } = 3;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!ServiceUriValidator.TryValidateAbsoluteOrServiceDiscoveryUri(BaseUri, out _))
        {
            yield return new ValidationResult(
                "CommandCenterWeb:BaseUri must be a valid absolute or Aspire service-discovery URI.",
                [nameof(BaseUri)]);
        }
    }
}
