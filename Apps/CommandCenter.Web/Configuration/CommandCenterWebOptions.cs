using System.ComponentModel.DataAnnotations;

namespace CommandCenter.Web.Configuration;

internal sealed class CommandCenterWebOptions : IValidatableObject
{
    internal const string SectionName = "CommandCenterWeb";

    [Required]
    public string BaseUri { get; set; } = string.Empty;

    [Required]
    public string OperationsAgentBaseUri { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public string AssetId { get; set; } = DemoAssets.StreetlightAssetId;

    [Range(1, 100)]
    public int SnapshotActivityLimit { get; set; } = 16;

    [Range(1, 60)]
    public int PollingIntervalSeconds { get; set; } = 3;

    public HostedAgentOptions HostedAgent { get; set; } = new();

    /// <summary>
    /// The Foundry-hosted twin of the Operations Agent. Unconfigured by default, deliberately: the
    /// project endpoint names a specific tenant, and nobody's cloud is written into this repository.
    /// Set <see cref="ProjectEndpoint"/> (user secrets or an environment variable) and the Command
    /// Center grows a "hosted habitat" panel; leave it empty and the local demo is exactly what it
    /// was.
    /// </summary>
    internal sealed class HostedAgentOptions
    {
        /// <summary>The Foundry project endpoint, e.g. https://&lt;account&gt;.services.ai.azure.com/api/projects/&lt;project&gt;.</summary>
        public string ProjectEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// Extra hosts the endpoint may name, for private-endpoint setups. Empty by default, and
        /// each entry is an exact host: the presenter's bearer token travels to this endpoint, so
        /// where it may go is an explicit decision, never a pattern.
        /// </summary>
        public IList<string> AllowedEndpointHosts { get; } = [];

        /// <summary>The hosted agent's name, as deploy-hosted-agent.yml created it.</summary>
        public string AgentName { get; set; } = "caesarea-operations";

        /// <summary>
        /// The Hosting stage's records question, asked by BOTH habitats so the contrast is exact:
        /// the local agent answers it from the simulated store, the hosted one from the work order
        /// in the presenter's own OneDrive through Work IQ. Asking where the record lives is what
        /// makes the source visible in the answer - the OneDrive copy carries a source-of-record
        /// line the simulated store does not.
        /// </summary>
        public string Question { get; set; } =
            "What do our maintenance records say about streetlight L-417? Name the work order you used, summarise what it requires, and state where the record lives.";

        public bool IsConfigured => !string.IsNullOrWhiteSpace(ProjectEndpoint);
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!ServiceUriValidator.TryValidateAbsoluteOrServiceDiscoveryUri(BaseUri, out _))
        {
            yield return new ValidationResult(
                "CommandCenterWeb:BaseUri must be a valid absolute or Aspire service-discovery URI.",
                [nameof(BaseUri)]);
        }

        if (!ServiceUriValidator.TryValidateAbsoluteOrServiceDiscoveryUri(OperationsAgentBaseUri, out _))
        {
            yield return new ValidationResult(
                "CommandCenterWeb:OperationsAgentBaseUri must be a valid absolute or Aspire service-discovery URI.",
                [nameof(OperationsAgentBaseUri)]);
        }

        // Only validated when set: an empty endpoint means the hosted panel is off, which is the
        // committed default. A set-but-wrong one fails at startup rather than as a mid-demo 404.
        if (HostedAgent.IsConfigured)
        {
            if (!Uri.TryCreate(HostedAgent.ProjectEndpoint, UriKind.Absolute, out var endpoint)
                || endpoint.Scheme != Uri.UriSchemeHttps)
            {
                yield return new ValidationResult(
                    "CommandCenterWeb:HostedAgent:ProjectEndpoint must be an absolute https URI.",
                    [nameof(HostedAgent)]);
            }
            else
            {
                // The presenter's own bearer token is attached to this endpoint, so a typo or a
                // tampered configuration value must fail at startup, not hand the token to
                // whichever HTTPS server the string happens to name. Standard Foundry hosting is
                // allowed by suffix; anything else must be listed host-by-host.
                var hostAllowed = endpoint.Host.EndsWith(".services.ai.azure.com", StringComparison.OrdinalIgnoreCase)
                    || HostedAgent.AllowedEndpointHosts.Any(host => string.Equals(host, endpoint.Host, StringComparison.OrdinalIgnoreCase));

                if (!hostAllowed)
                {
                    yield return new ValidationResult(
                        $"CommandCenterWeb:HostedAgent:ProjectEndpoint names host '{endpoint.Host}', which is neither *.services.ai.azure.com nor listed in HostedAgent:AllowedEndpointHosts. The presenter's token would be sent there.",
                        [nameof(HostedAgent)]);
                }

                if (!string.IsNullOrEmpty(endpoint.UserInfo)
                    || !string.IsNullOrEmpty(endpoint.Query)
                    || !string.IsNullOrEmpty(endpoint.Fragment))
                {
                    yield return new ValidationResult(
                        "CommandCenterWeb:HostedAgent:ProjectEndpoint must carry no user-info, query or fragment.",
                        [nameof(HostedAgent)]);
                }

                if (!System.Text.RegularExpressions.Regex.IsMatch(
                    endpoint.AbsolutePath, "^/api/projects/[^/]+/?$", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1)))
                {
                    yield return new ValidationResult(
                        "CommandCenterWeb:HostedAgent:ProjectEndpoint must be a project endpoint of the form https://<host>/api/projects/<project>.",
                        [nameof(HostedAgent)]);
                }
            }

            if (string.IsNullOrWhiteSpace(HostedAgent.AgentName))
            {
                yield return new ValidationResult(
                    "CommandCenterWeb:HostedAgent:AgentName must be set when a project endpoint is configured.",
                    [nameof(HostedAgent)]);
            }

            if (string.IsNullOrWhiteSpace(HostedAgent.Question))
            {
                yield return new ValidationResult(
                    "CommandCenterWeb:HostedAgent:Question must be set when a project endpoint is configured.",
                    [nameof(HostedAgent)]);
            }
        }
    }
}
