using System.Net.NetworkInformation;

namespace OperationsAgent.Hosted;

/// <summary>
/// Reports, once, what the hosting platform actually handed this container.
/// </summary>
/// <remarks>
/// What <c>AgentHost</c> writes about its environment during construction goes to a bootstrap logger
/// that is gone before Application Insights is wired, so a container that dies at startup leaves no
/// evidence but the crash. A hosted service runs after the server has bound and logging is real -
/// late enough to be seen, early enough to precede any session. Names only, never values: which
/// variables a platform injects is what you need to know, and a value could be a connection string.
/// <c>PORT</c> is named explicitly because its value decides whether this process starts at all.
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
