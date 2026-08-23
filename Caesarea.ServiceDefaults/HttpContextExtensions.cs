using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Caesarea.ServiceDefaults;

/// <summary>
/// Provides correlation-aware helpers for ASP.NET Core HTTP contexts.
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Gets the current request correlation identifier, creating and storing one when the request pipeline has not already assigned it.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>The correlation identifier associated with the request.</returns>
    public static string GetCorrelationId(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Items.TryGetValue(CorrelationIds.HeaderName, out var value)
            && value is string correlationId
            && !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId;
        }

        var createdCorrelationId = CorrelationIds.Create();
        context.Items[CorrelationIds.HeaderName] = createdCorrelationId;
        context.Response.Headers[CorrelationIds.HeaderName] = createdCorrelationId;
        return createdCorrelationId;
    }
}

/// <summary>
/// Creates and names correlation identifiers used across deterministic Stage 0 service boundaries.
/// </summary>
public static class CorrelationIds
{
    /// <summary>
    /// Gets the header name used to transport the current correlation identifier.
    /// </summary>
    public const string HeaderName = "X-Correlation-ID";

    /// <summary>
    /// Creates a new correlation identifier value.
    /// </summary>
    /// <returns>A new opaque correlation identifier.</returns>
    public static string Create() => Guid.NewGuid().ToString("N");
}

/// <summary>
/// Exposes well-known correlation header names for HTTP client and server code.
/// </summary>
public static class CorrelationHeaderNames
{
    /// <summary>
    /// Gets the HTTP header name used for the Caesarea correlation identifier.
    /// </summary>
    public const string XCorrelationId = CorrelationIds.HeaderName;
}

/// <summary>
/// Creates consistent problem details payloads for minimal API responses.
/// </summary>
public static class ProblemDetailsFactory
{
    /// <summary>
    /// Creates a problem details payload that includes the current correlation identifier.
    /// </summary>
    /// <param name="statusCode">The HTTP status code returned to the caller.</param>
    /// <param name="title">The short problem title.</param>
    /// <param name="detail">The detailed problem description.</param>
    /// <param name="correlationId">The correlation identifier for the current request.</param>
    /// <returns>A populated <see cref="ProblemDetails"/> instance.</returns>
    public static ProblemDetails Create(int statusCode, string title, string detail, string correlationId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(statusCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };

        problem.Extensions["correlationId"] = correlationId;

        return problem;
    }

    /// <summary>
    /// Creates a validation problem details payload that includes the current correlation identifier.
    /// </summary>
    /// <param name="errors">The keyed validation errors to return.</param>
    /// <param name="correlationId">The correlation identifier for the current request.</param>
    /// <param name="title">The short validation problem title.</param>
    /// <param name="detail">The detailed validation problem description.</param>
    /// <returns>A populated <see cref="ValidationProblemDetails"/> instance.</returns>
    public static ValidationProblemDetails CreateValidationProblem(
        IDictionary<string, string[]> errors,
        string correlationId,
        string title = "Request validation failed",
        string detail = "One or more validation errors occurred.")
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);

        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = title,
            Detail = detail
        };

        problem.Extensions["correlationId"] = correlationId;

        return problem;
    }
}
