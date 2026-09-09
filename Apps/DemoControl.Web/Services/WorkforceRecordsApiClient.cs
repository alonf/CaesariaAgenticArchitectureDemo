using System.Text.Json;

namespace DemoControl.Web.Services;

/// <summary>
/// Reads the workforce domain's work orders in full, for the presenter's own view.
/// <para>
/// This is the one client in the solution that sees a technician's name, badge, labour cost and
/// rate, and it exists so the lecture can put those fields on screen beside the answer the peer
/// agent gave without them. The Workforce Hub serves this route to loopback callers only - the
/// switchboard runs on the presenter's machine and names itself, and no agent can reach it. The
/// caller header is demo-grade identification - a name, not an authenticated principal - and it is
/// paired with the hub's loopback check because neither condition alone is a boundary.
/// </para>
/// </summary>
internal sealed class WorkforceRecordsApiClient(HttpClient httpClient)
{
    // The Workforce Hub's own constants live in that service; naming them here keeps this client
    // from taking a project reference on a hub it is only allowed to read one route of.
    private const string CallerHeaderName = "X-Caesarea-Caller";
    private const string CallerName = "demo-control";

    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    /// <summary>
    /// Reads every work order the domain holds, in full.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The work orders, newest first.</returns>
    public async Task<IReadOnlyList<WorkOrderRecord>> GetAllAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/workforce-records");
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, CorrelationIds.Create());
        request.Headers.Add(CallerHeaderName, CallerName);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        await response.EnsureSuccessAsync(cancellationToken);

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<WorkOrderRecord>>(SerializerOptions, cancellationToken)
            ?? [];
    }
}
