using System.ComponentModel.DataAnnotations;

namespace DemoControl.Web.Configuration;

internal sealed class DemoControlWebOptions : IValidatableObject
{
    internal const string SectionName = "DemoControlWeb";

    [Required]
    public string BaseUri { get; set; } = string.Empty;

    [Required]
    public string OperationsAgentBaseUri { get; set; } = string.Empty;

    [Range(1, 60)]
    public int PollingIntervalSeconds { get; set; } = 4;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!ServiceUriValidator.TryValidateAbsoluteOrServiceDiscoveryUri(BaseUri, out _))
        {
            yield return new ValidationResult(
                "DemoControlWeb:BaseUri must be a valid absolute or Aspire service-discovery URI.",
                [nameof(BaseUri)]);
        }

        if (!ServiceUriValidator.TryValidateAbsoluteOrServiceDiscoveryUri(OperationsAgentBaseUri, out _))
        {
            yield return new ValidationResult(
                "DemoControlWeb:OperationsAgentBaseUri must be a valid absolute or Aspire service-discovery URI.",
                [nameof(OperationsAgentBaseUri)]);
        }
    }
}
