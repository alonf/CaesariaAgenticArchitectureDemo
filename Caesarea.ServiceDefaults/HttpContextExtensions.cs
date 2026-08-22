using Caesarea.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Caesarea.ServiceDefaults;

public static class HttpContextExtensions
{
    public static string GetCorrelationId(this HttpContext context)
    {
        if (context.Items.TryGetValue(CorrelationHeaderNames.XCorrelationId, out var value)
            && value is string correlationId
            && !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId;
        }

        return CorrelationIds.Create();
    }
}

public static class ProblemDetailsFactory
{
    public static ProblemDetails Create(int statusCode, string title, string detail, string correlationId)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };

        problem.Extensions["correlationId"] = correlationId;

        return problem;
    }
}
