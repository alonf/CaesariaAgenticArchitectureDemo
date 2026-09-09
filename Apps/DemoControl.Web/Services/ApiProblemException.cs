using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace DemoControl.Web.Services;

/// <summary>
/// A service's refusal, as the service explained it. The status line alone - "409 (Conflict)" -
/// tells the presenter nothing; the problem details behind it say what was refused and why, and a
/// stage-gated refusal also names the stage that would allow it, so the switchboard can offer to
/// go there.
/// </summary>
internal sealed class ApiProblemException(
    HttpStatusCode statusCode,
    string message,
    string? title,
    string? detail,
    DemoStage? requiredStage,
    string? correlationId) : HttpRequestException(message, null, statusCode)
{
    /// <summary>Gets the problem's short title, when the service sent one.</summary>
    public string? Title { get; } = title;

    /// <summary>Gets the problem's explanation, when the service sent one.</summary>
    public string? Detail { get; } = detail;

    /// <summary>Gets the stage the refused capability requires, when the refusal was a stage gate.</summary>
    public DemoStage? RequiredStage { get; } = requiredStage;

    /// <summary>Gets the correlation identifier of the refused request, for the logs.</summary>
    public string? CorrelationId { get; } = correlationId;
}

/// <summary>
/// Turns a non-success response into an <see cref="ApiProblemException"/> that carries the
/// service's own explanation.
/// </summary>
internal static class HttpResponseProblemExtensions
{
    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    /// <summary>
    /// Throws for a non-success response, with the service's explanation when it sent one.
    /// </summary>
    /// <param name="response">The response to check.</param>
    /// <param name="cancellationToken">A token to cancel reading the body.</param>
    public static async Task EnsureSuccessAsync(this HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!response.IsSuccessStatusCode)
        {
            throw await ReadProblemAsync(response, cancellationToken);
        }
    }

    /// <summary>
    /// Reads a non-success response's problem details. A body that is not a problem document - a
    /// gateway's HTML, nothing at all - leaves the status line as the whole explanation.
    /// </summary>
    /// <param name="response">The non-success response.</param>
    /// <param name="cancellationToken">A token to cancel reading the body.</param>
    /// <returns>The exception describing the refusal.</returns>
    public static async Task<ApiProblemException> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);

        ProblemDetails? problem = null;

        try
        {
            problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(SerializerOptions, cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            // Not a problem document.
        }

        var statusLine = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}".TrimEnd();
        var correlationId = response.Headers.TryGetValues(CorrelationHeaderNames.XCorrelationId, out var values)
            ? values.FirstOrDefault()
            : null;

        return new ApiProblemException(
            response.StatusCode,
            problem?.Detail ?? problem?.Title ?? statusLine,
            problem?.Title,
            problem?.Detail,
            ReadRequiredStage(problem),
            correlationId);
    }

    private static DemoStage? ReadRequiredStage(ProblemDetails? problem) =>
        problem is not null
        && problem.Extensions.TryGetValue(DemoStageProblemExtensions.RequiredStage, out var value)
        && value is JsonElement { ValueKind: JsonValueKind.String } element
        && Enum.TryParse<DemoStage>(element.GetString(), ignoreCase: false, out var stage)
        && Enum.IsDefined(stage)
            ? stage
            : null;
}
