namespace WorkforceHub.Api.Services;

/// <summary>
/// The Workforce Hub's access rule. Every request must name a caller, and each route group admits
/// only the callers that belong there: the workforce domain's own agent may read work orders, the
/// presenter's scenario service may seed them, and nobody may do both.
/// <para>
/// Demo-grade, like its Security counterpart: a header, not an authenticated principal. It is
/// enforced rather than assumed so that the boundary this stage claims is a rule the service
/// applies. The Governance stage replaces it with real identity.
/// </para>
/// </summary>
public static class CallerIdentity
{
    /// <summary>The header naming the calling service.</summary>
    public const string HeaderName = "X-Caesarea-Caller";

    /// <summary>The Workforce Agent: the only caller permitted to read work orders.</summary>
    public const string WorkforceAgent = "workforce-agent";

    /// <summary>The scenario service: the only caller permitted to seed or clear work orders.</summary>
    public const string DemoScenario = "demo-scenario";

    /// <summary>
    /// Rejects the request unless it names one of the permitted callers.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="permitted">The callers this route group admits.</param>
    /// <returns>A problem result when the caller is not permitted; otherwise <see langword="null"/>.</returns>
    public static IResult? Reject(HttpContext context, params string[] permitted)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(permitted);

        var caller = context.Request.Headers[HeaderName].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(caller) && permitted.Contains(caller, StringComparer.Ordinal))
        {
            return null;
        }

        return Results.Problem(ProblemDetailsFactory.Create(
            StatusCodes.Status403Forbidden,
            "Caller not permitted",
            $"The Workforce Hub admits {string.Join(" or ", permitted)} on this route; the request identified itself as '{caller ?? "nobody"}'.",
            context.GetCorrelationId()));
    }
}
