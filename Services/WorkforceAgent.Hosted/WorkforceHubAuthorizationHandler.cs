using System.Net.Http.Headers;
using Azure.Core;

namespace WorkforceAgent.Hosted;

/// <summary>
/// Attaches the agent's own Entra token to every call the Workforce Hub gateway makes.
/// </summary>
/// <remarks>
/// A message handler rather than a change to <c>HttpWorkforceHubGateway</c>, for the same reason the
/// Operations agent does it this way: the gateway is shared with the Aspire-hosted agent, where the
/// Hub is a neighbour on a private network and needs no token. Teaching the gateway to acquire one
/// would make the shared code aware of which habitat it is in, which is the claim this project
/// exists to disprove. The host decides how calls are authenticated; the gateway keeps describing
/// only what it wants to read.
/// </remarks>
internal sealed class WorkforceHubAuthorizationHandler(
    TokenCredential credential,
    string scope,
    ILogger<WorkforceHubAuthorizationHandler> logger) : DelegatingHandler
{
    // Refreshed a little before expiry. Renewing exactly at expiry loses the race often enough to
    // matter, and the resulting 401 reads like a permissions problem rather than a clock one.
    private static readonly TimeSpan RenewBefore = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private AccessToken _token;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await GetTokenAsync(cancellationToken));

        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_token.ExpiresOn > DateTimeOffset.UtcNow + RenewBefore)
        {
            return _token.Token;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            // Checked again inside the gate: a delegated task can fan out into several tool calls at
            // once, and without this they would each mint a token and race to store it.
            if (_token.ExpiresOn > DateTimeOffset.UtcNow + RenewBefore)
            {
                return _token.Token;
            }

            try
            {
                _token = await credential.GetTokenAsync(new TokenRequestContext([scope]), cancellationToken);
            }
            catch (Exception exception)
            {
                // Named, because without this the failure surfaces as a 401 from the Hub, which reads
                // like a misconfigured ingress and is not one.
                WorkforceHostedLog.WorkforceHubTokenFailed(logger, scope, exception);
                throw;
            }

            return _token.Token;
        }
        finally
        {
            _gate.Release();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _gate.Dispose();
        }

        base.Dispose(disposing);
    }
}

internal static partial class WorkforceHostedLog
{
    [LoggerMessage(
        EventId = 2800,
        Level = LogLevel.Error,
        Message = "Could not acquire a token for the Workforce Hub ({Scope}). The agent has an identity but not this access.")]
    internal static partial void WorkforceHubTokenFailed(ILogger logger, string scope, Exception exception);
}
