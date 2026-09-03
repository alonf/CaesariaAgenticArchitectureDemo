using System.Text.Json;

namespace WorkforceAgent.Api.Services;

/// <summary>
/// Reads the Workforce Hub. This is the only client of that hub in the system, and it deliberately
/// exposes no way to fetch a work order in full - there is no method here that could return a rate
/// or a technician's name, so nothing downstream can ask for one.
/// </summary>
public interface IWorkforceHubGateway
{
    /// <summary>
    /// Finds the work orders raised for an asset.
    /// </summary>
    /// <param name="assetId">The asset to search for.</param>
    /// <param name="correlationId">The correlation identifier spanning the delegated task.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The matching work order summaries, newest first.</returns>
    public Task<IReadOnlyList<WorkOrderSummary>> FindForAssetAsync(
        string assetId, string correlationId, CancellationToken cancellationToken);

    /// <summary>
    /// Opens one work order, in its shareable projection only.
    /// </summary>
    /// <param name="workOrderId">The work order to open.</param>
    /// <param name="correlationId">The correlation identifier spanning the delegated task.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The shareable details, or <see langword="null"/> when no such work order exists.</returns>
    public Task<ShareableWorkOrderDetails?> GetShareableDetailsAsync(
        string workOrderId, string correlationId, CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class HttpWorkforceHubGateway(HttpClient httpClient) : IWorkforceHubGateway
{
    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkOrderSummary>> FindForAssetAsync(
        string assetId, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        using var request = CreateRequest(
            HttpMethod.Get, $"/api/workforce/assets/{Uri.EscapeDataString(assetId)}/work-orders", correlationId);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<WorkOrderSummary>>(SerializerOptions, cancellationToken)
            ?? [];
    }

    /// <inheritdoc />
    public async Task<ShareableWorkOrderDetails?> GetShareableDetailsAsync(
        string workOrderId, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workOrderId);

        using var request = CreateRequest(
            HttpMethod.Get, $"/api/workforce/work-orders/{Uri.EscapeDataString(workOrderId)}/shareable", correlationId);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ShareableWorkOrderDetails>(SerializerOptions, cancellationToken);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string relativeUri, string correlationId)
    {
        var request = new HttpRequestMessage(method, relativeUri);
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, correlationId);
        request.Headers.Add("X-Caesarea-Caller", "workforce-agent");
        return request;
    }
}
