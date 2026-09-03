namespace OperationsAgent.Api.Services;

/// <summary>
/// Wakes the Security Operations Agent's credential chain when the MultiAgent stage is reached.
/// See <see cref="PeerAgentWarmup"/> for why this exists.
/// </summary>
public sealed class SecurityAgentWarmup(
    IHttpClientFactory httpClientFactory,
    ILogger<SecurityAgentWarmup> logger) : PeerAgentWarmup(httpClientFactory, logger)
{
    /// <summary>
    /// The named client addressing the Security Operations Agent.
    /// </summary>
    public const string HttpClientName = "securityagent-warmup";

    /// <inheritdoc />
    protected override string ClientName => HttpClientName;

    /// <inheritdoc />
    protected override string PeerName => "the Security Operations Agent";
}
