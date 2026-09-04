using System.Net.NetworkInformation;

namespace OperationsAgent.Hosted;

/// <summary>
/// Reports, once, what the hosting platform actually handed this container.
/// </summary>
/// <remarks>
/// This exists because the first hosted deployment failed for a reason nothing could see. The
/// container died with "Failed to bind to address http://0.0.0.0:8088: address already in use", and
/// everything <c>AgentHost</c> writes about its environment during construction goes to a bootstrap
/// logger that is gone before Application Insights is wired - so the only surviving evidence was the
/// crash itself, and the session error blamed the <c>/readiness</c> endpoint that never got to answer.
///
/// A hosted service runs after the server has bound and after logging is real, which is exactly late
/// enough to be useful and early enough to run before any session arrives.
///
/// Names only, never values. Which variables a platform injects is precisely what you need to know
/// when running a container you did not configure, and also the last place you want to discover you
/// have written a connection string into a trace. <c>PORT</c> is named explicitly because it is the
/// one whose value decides whether this process starts at all.
/// </remarks>
internal sealed class HostingDiagnostics(ILogger<HostingDiagnostics> logger, string? injectedPort) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var names = string.Join(
            ", ",
            Environment.GetEnvironmentVariables().Keys.Cast<string>().Order(StringComparer.Ordinal));

        HostedAgentLog.HostingEnvironment(logger, injectedPort ?? "(not set)", names);

        // Who else is listening in this sandbox, read from the network namespace this process shares.
        // The container failed to bind both the injected port and the obvious alternative, which
        // means something was there first - and from inside a sandbox with no shell, this is the only
        // way to find out what. It runs before the server binds, so it still reports the ports that
        // are about to collide.
        try
        {
            var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            HostedAgentLog.ActiveListeners(
                logger,
                listeners.Length == 0
                    ? "(none)"
                    : string.Join(", ", listeners.Select(endpoint => endpoint.ToString()).Order(StringComparer.Ordinal)));
        }
        catch (Exception exception)
        {
            // Diagnostics must never be the reason a container does not start.
            HostedAgentLog.ActiveListenersUnavailable(logger, exception);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
