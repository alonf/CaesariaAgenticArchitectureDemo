using A2A;

namespace WorkforceAgent.Api.Services;

/// <summary>
/// The agent card this domain publishes.
/// <para>
/// This is what makes the delegation an agent relationship rather than a call: another domain
/// discovers a named agent with a provider, a version and declared skills, and decides whether to
/// consult it - it does not receive a function signature to invoke. The card is also where the
/// boundary is advertised: the skill says plainly what it will not disclose, so a caller learns
/// the limit at discovery time instead of meeting it as a refusal at runtime.
/// </para>
/// </summary>
public static class WorkforceAgentCard
{
    /// <summary>The A2A well-known location the standard card resolver reads.</summary>
    public const string WellKnownPath = "/.well-known/agent-card.json";

    private static readonly string ProviderUrl = string.Join(
        "://", "https", "caesarea.example.gov/workforce");

    /// <summary>
    /// Builds the card for the supplied public base address.
    /// </summary>
    /// <param name="agentName">The agent's published identity.</param>
    /// <param name="baseAddress">The public base address this agent is reachable at.</param>
    /// <returns>The agent card.</returns>
    public static AgentCard Create(string agentName, Uri baseAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        ArgumentNullException.ThrowIfNull(baseAddress);

        return new AgentCard
        {
            Name = agentName,
            Description = "Answers other city domains' questions about an asset's maintenance work orders: why an "
                + "asset is in its current state, what is being done about it, and when it is expected to return to "
                + "normal. Commercial and personal details of a work order - labour cost, contracted rates and the "
                + "identity of the technician dispatched - are not available through this agent.",
            Version = "1.0.0",
            // The provider URL is the domain's own public page; it is documentation on the card,
            // not an address this demo calls.
            Provider = new AgentProvider
            {
                Organization = "Caesarea Smart City - Workforce Management",
                Url = ProviderUrl
            },
            Capabilities = new AgentCapabilities { Streaming = true },
            DefaultInputModes = ["text/plain"],
            DefaultOutputModes = ["text/plain"],
            SupportedInterfaces =
            [
                new AgentInterface
                {
                    Url = new Uri(baseAddress, "a2a").ToString(),
                    ProtocolBinding = "HTTP+JSON"
                }
            ],
            Skills =
            [
                new AgentSkill
                {
                    Id = "asset-maintenance-situation",
                    Name = "Asset maintenance situation",
                    Description = "Finds the work orders raised for an asset, identifies the one that explains its "
                        + "current state, and reports the reason, the status and the expected clearance time. Does "
                        + "not disclose labour cost, contracted rates or technician identity.",
                    Tags = ["maintenance", "work-orders", "operations"],
                    Examples =
                    [
                        "Do you have any work order related to L-417, and does it explain why it is still overridden?",
                        "When is the manual override on L-417 expected to be cleared?"
                    ],
                    InputModes = ["text/plain"],
                    OutputModes = ["text/plain"]
                }
            ]
        };
    }
}
