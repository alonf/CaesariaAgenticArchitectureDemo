namespace OperationsAgent.Api.Services;

/// <summary>
/// Wakes the Caesarea Workforce Agent's credential chain when the A2A Delegation stage is reached.
/// See <see cref="PeerAgentWarmup"/> for why this exists.
/// </summary>
public sealed class WorkforceAgentWarmup(
    IHttpClientFactory httpClientFactory,
    ILogger<WorkforceAgentWarmup> logger) : PeerAgentWarmup(httpClientFactory, logger)
{
    /// <summary>
    /// The named client addressing the Caesarea Workforce Agent.
    /// </summary>
    public const string HttpClientName = "workforceagent-warmup";

    /// <inheritdoc />
    protected override string ClientName => HttpClientName;

    /// <inheritdoc />
    protected override string PeerName => "the Caesarea Workforce Agent";
}
