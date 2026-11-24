using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Caesarea.ServiceDefaults;

/// <summary>Resolves correlation at tool invocation time without modifying a streaming response.</summary>
public static class InvocationCorrelation
{
    /// <summary>Uses the incoming correlation, current trace, or HTTP request identity, in that order.</summary>
    public static string Resolve(HttpContext? context)
    {
        var incoming = context?.Request.Headers[CorrelationIds.HeaderName].FirstOrDefault();
        var correlationId = incoming is { Length: > 0 and <= 128 }
            && !string.IsNullOrWhiteSpace(incoming) && !incoming.Any(char.IsControl)
            ? incoming
            : Activity.Current?.TraceId.ToString() ?? context?.TraceIdentifier ?? CorrelationIds.Create();
        Activity.Current?.SetTag("caesarea.correlation_id", correlationId);
        return correlationId;
    }
}
