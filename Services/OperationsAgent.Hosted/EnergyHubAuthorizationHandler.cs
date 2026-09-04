using System.Net.Http.Headers;
using Azure.Core;

namespace OperationsAgent.Hosted;

/// <summary>
/// Attaches the agent's own Entra token to every call the Energy Hub gateway makes.
/// </summary>
/// <remarks>
/// This is a message handler rather than a change to <c>HttpEnergyReadGateway</c>, and the
/// distinction carries the whole "same agent, different habitat" claim. The gateway is shared with
/// the Aspire-hosted agent, where the Energy Hub is a neighbour on a private network and needs no
/// token. Teaching the gateway to acquire one would mean the shared code knows which habitat it is
/// in - which is exactly the thing this project exists to disprove. Composing a handler around it
/// instead means the host decides how calls are authenticated, and the gateway keeps describing
/// only what it wants to read.
///
/// The credential is <c>DefaultAzureCredential</c>, which in the sandbox resolves to the dedicated
/// Entra identity the platform mints for this agent. No secret exists anywhere in this path: the
/// agent proves who it is, the ingress checks the audience, and the app role decides whether that
/// identity is allowed in.
/// </remarks>
internal sealed class EnergyHubAuthorizationHandler(
    TokenCredential credential,
    string scope,
    ILogger<EnergyHubAuthorizationHandler> logger) : DelegatingHandler
{
    // Refreshed a little before it expires. Renewing exactly at expiry loses the race often enough
    // to matter, and the resulting 401 looks like a permissions problem rather than a clock one.
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
            // Checked again inside the gate: several tool calls can arrive together, and without
            // this they would each mint a token and race to store it.
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
                // Loud, and named. Without this the failure surfaces as a 401 from the Energy Hub,
                // which reads like a misconfigured ingress and is not one.
                HostedAgentLog.EnergyHubTokenFailed(logger, scope, exception);
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
