using A2A;

namespace WorkforceAgent.Api.Services;

/// <summary>
/// The agent card this domain publishes.
/// <para>
/// This is what makes the delegation an agent relationship rather than a call: another domain
/// discovers a named agent with a provider, a version and declared skills, and decides whether to
/// consult it - it does not receive a function signature to invoke. The card also states the
/// boundary, so a caller learns the limit at discovery time instead of meeting it as a refusal at
/// runtime.
/// </para>
/// <para>
/// What the card is not is the boundary itself. Every sentence here is documentation: delete the
/// whole description and nothing leaks, because what makes the commercial and personal fields
/// unreachable is the projection the tools return, one service away. The wording says "not in the
/// records this agent reads" rather than "will not disclose" for exactly that reason - the first is
/// a fact a caller can rely on, the second is a promise a caller will try to argue with.
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
            // Three sentences, in this order on purpose: which domain this is and what it owns,
            // what it will answer, and what it holds. The middle sentence is narrower than the
            // first - the domain owns far more than this agent exposes - and the third is a
            // statement about this agent's records, not a promise about its behaviour.
            Description = "The Caesarea workforce domain: the city's maintenance work orders and the technicians "
                + "who carry them out. This agent answers other city domains' questions about one asset - why it is "
                + "in its current state, what is being done about it, and when it is expected to return to normal. "
                + "It reads only the shareable projection of a work order, so the commercial and personal detail the "
                + "domain holds - labour cost, contracted rates, technician identity - is not part of the records "
                + "available to it.",
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
                    // The trailing slash is load-bearing. A client resolves the protocol's method
                    // paths relatively against this URL, and against ".../a2a" the last segment is
                    // replaced rather than extended - every call would land one level too high and
                    // come back 404. Published as a directory, it is what the routes hang off.
                    Url = new Uri(baseAddress, "a2a/").ToString(),
                    // Named from the protocol's own constant, because the consulting side reads
                    // this to decide which transport client to build. A binding written by hand
                    // that does not match is a peer nobody can talk to.
                    ProtocolBinding = ProtocolBindingNames.HttpJson
                }
            ],
            Skills =
            [
                new AgentSkill
                {
                    Id = "asset-maintenance-situation",
                    Name = "Asset maintenance situation",
                    // "Are not in the records this skill reads" rather than "will not disclose".
                    // The limit is a fact about what crosses into this agent, not a policy it
                    // enforces - and a caller who reads it as a policy will try to argue with it.
                    Description = "Finds the work orders raised for an asset, identifies the one that explains its "
                        + "current state, and reports the reason, the status and the expected clearance time. Labour "
                        + "cost, contracted rates and technician identity are not in the records this skill reads.",
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
