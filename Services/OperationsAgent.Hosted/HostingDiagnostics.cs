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

        var listeners = DescribeListeners();

        HostedAgentLog.HostingEnvironment(logger, injectedPort ?? "(not set)", names);
        HostedAgentLog.ActiveListeners(logger, listeners);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Every TCP endpoint listening in this process's network namespace, as a single line.
    /// </summary>
    /// <remarks>
    /// From inside a sandbox with no shell, this is the only way to see what already holds a port.
    /// It never throws: a diagnostic must not be the reason a container fails to start, and it is
    /// called from a catch block where a second exception would replace the first.
    /// </remarks>
    internal static string DescribeListeners()
    {
        try
        {
            var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();

            return listeners.Length == 0
                ? "(none)"
                : string.Join(", ", listeners.Select(endpoint => endpoint.ToString()).Order(StringComparer.Ordinal));
        }
        catch (Exception exception)
        {
            return $"(unavailable: {exception.GetType().Name})";
        }
    }
}
