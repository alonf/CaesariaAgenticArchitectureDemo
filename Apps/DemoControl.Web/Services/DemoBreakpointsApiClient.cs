using System.Text.Json;

namespace DemoControl.Web.Services;

/// <summary>
/// Reads and arms demo breakpoints across every service that owns a snippet. Snippets live where
/// the code they pause lives - the MCP server and the paused remote tool in the Energy Hub, the
/// consulted second agent in the Security Agent - so a switchboard that talked to one service could
/// not arm the newest stage's snippet at all.
/// </summary>
internal sealed class DemoBreakpointsApiClient(IHttpClientFactory httpClientFactory)
{
    /// <summary>
    /// The named HTTP client and debuggable process for each service that registers snippets.
    /// </summary>
    internal static readonly IReadOnlyList<DemoBreakpointService> Services =
    [
        new("Operations Agent", "breakpoints-operationsagent", "OperationsAgent.Api.exe"),
        new("Energy Hub", "breakpoints-energyhub", "EnergyHub.Api.exe"),
        new("Security Agent", "breakpoints-securityagent", "SecurityAgent.Api.exe"),
        new("Workforce Agent", "breakpoints-workforceagent", "WorkforceAgent.Api.exe")
    ];

    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    // Short: this panel polls, and a presenter waiting on it is waiting mid-talk.
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Reads every service's breakpoint state. A service that is not running degrades to its own
    /// row carrying the error: one absent service must not blank the whole panel.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>One source per service, in presentation order.</returns>
    public async Task<IReadOnlyList<DemoBreakpointSource>> GetStatusAsync(CancellationToken cancellationToken)
    {
        var reads = Services.Select(service => ReadSourceAsync(service, cancellationToken));
        return await Task.WhenAll(reads);
    }

    /// <summary>
    /// Arms or disarms one snippet on the service that owns it.
    /// </summary>
    /// <param name="clientName">The owning service's named HTTP client.</param>
    /// <param name="snippetName">The snippet to arm.</param>
    /// <param name="armed">Whether the snippet should pause on its next debugged run.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The owning service's refreshed state.</returns>
    public async Task<DemoBreakpointsResponse> SetArmedAsync(
        string clientName, string snippetName, bool armed, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);
        ArgumentException.ThrowIfNullOrWhiteSpace(snippetName);

        using var client = CreateClient(clientName);
        using var request = CreateRequest(HttpMethod.Put, $"/api/demo-breakpoints/{Uri.EscapeDataString(snippetName)}");
        request.Content = JsonContent.Create(new DemoBreakpointArmRequest(armed), options: SerializerOptions);
        using var response = await client.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<DemoBreakpointsResponse>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Demo breakpoints response was empty.");
    }

    private async Task<DemoBreakpointSource> ReadSourceAsync(DemoBreakpointService service, CancellationToken cancellationToken)
    {
        // A service that hangs must cost this panel one row, not the whole panel: without its own
        // deadline the read would sit on the HttpClient's default timeout with every other row
        // waiting behind it.
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        attempt.CancelAfter(ReadTimeout);

        try
        {
            using var client = CreateClient(service.ClientName);
            using var request = CreateRequest(HttpMethod.Get, "/api/demo-breakpoints");
            using var response = await client.SendAsync(request, attempt.Token);

            response.EnsureSuccessStatusCode();

            var status = await response.Content.ReadFromJsonAsync<DemoBreakpointsResponse>(SerializerOptions, attempt.Token)
                ?? throw new InvalidOperationException("Demo breakpoints response was empty.");

            return new DemoBreakpointSource(service, status.DebuggerAttached, status.Snippets, Error: null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller gave up - the page is going away, so let that propagate.
            throw;
        }
        catch (Exception exception)
        {
            // Everything else is this service's problem, including a timeout: an HttpClient
            // deadline surfaces as a TaskCanceledException, and treating that as cancellation
            // failed the whole fan-out and blanked every other service's row.
            var reason = exception is OperationCanceledException
                ? $"{service.DisplayName} did not respond within {ReadTimeout.TotalSeconds:0} seconds."
                : exception.Message;

            return new DemoBreakpointSource(service, DebuggerAttached: false, Snippets: [], Error: reason);
        }
    }

    private HttpClient CreateClient(string clientName) => httpClientFactory.CreateClient(clientName);

    private static HttpRequestMessage CreateRequest(HttpMethod method, string relativeUri)
    {
        var request = new HttpRequestMessage(method, relativeUri);
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, CorrelationIds.Create());
        return request;
    }
}

/// <summary>
/// A service that registers demo snippets.
/// </summary>
/// <param name="DisplayName">The projector-friendly service name.</param>
/// <param name="ClientName">The named HTTP client addressing the service.</param>
/// <param name="ProcessName">The process a debugger attaches to for this service's snippets.</param>
internal sealed record DemoBreakpointService(string DisplayName, string ClientName, string ProcessName);

/// <summary>
/// One service's breakpoint state, or the reason it could not be read.
/// </summary>
/// <param name="Service">The service this state belongs to.</param>
/// <param name="DebuggerAttached">Whether a debugger is attached to that service's process.</param>
/// <param name="Snippets">The snippets the service registered.</param>
/// <param name="Error">Why the service could not be reached, when it could not.</param>
internal sealed record DemoBreakpointSource(
    DemoBreakpointService Service,
    bool DebuggerAttached,
    IReadOnlyList<DemoBreakpointStatus> Snippets,
    string? Error);
