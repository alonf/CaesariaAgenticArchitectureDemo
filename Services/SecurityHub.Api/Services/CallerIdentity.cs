namespace SecurityHub.Api.Services;

/// <summary>
/// The Security Hub's access rule. Every request must name a caller, and each route group admits
/// only the callers that belong there: the Security Agent may read operations, the presenter's
/// scenario service may seed them, and nobody may do both.
/// <para>
/// This is a demo-grade stand-in for real identity - a header, not an authenticated principal -
/// and it is deliberately enforced rather than assumed, so the boundary the MultiAgent stage
/// claims is a rule the service applies, not a convention the callers happen to follow. The
/// Governance stage replaces it with real identity.
/// </para>
/// </summary>
public static class CallerIdentity
{
    /// <summary>The header naming the calling service.</summary>
    public const string HeaderName = "X-Caesarea-Caller";

    /// <summary>The Security Agent: the only caller permitted to read operations.</summary>
    public const string SecurityAgent = "security-agent";

    /// <summary>The scenario service: the only caller permitted to seed or clear operations.</summary>
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
            $"The Security Hub admits {string.Join(" or ", permitted)} on this route; the request identified itself as '{caller ?? "nobody"}'.",
            context.GetCorrelationId()));
    }
}
