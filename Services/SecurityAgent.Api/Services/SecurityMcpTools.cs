using System.ComponentModel;
using ModelContextProtocol.Server;

namespace SecurityAgent.Api.Services;

/// <summary>
/// Exposes the Security Agent to other domains as a capability over MCP - the remote boundary
/// from slide 40's composition matrix. The relationship is delegation: the caller asks a
/// question and keeps ownership of its own answer. What crosses back is a sanitized assessment,
/// never the security records the agent reasoned over.
/// </summary>
[McpServerToolType]
public sealed class SecurityMcpTools(SecurityAssessmentAgent assessmentAgent, IHttpContextAccessor httpContextAccessor)
{
    /// <summary>
    /// The stable name of the cross-domain consult capability.
    /// </summary>
    public const string AssessLightingRequirementToolName = "assess_lighting_requirement";

    /// <summary>
    /// Asks the Security Operations Agent whether an active operation requires lighting in an area.
    /// </summary>
    /// <param name="area">The area to assess.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The sanitized lighting assessment.</returns>
    [McpServerTool(Name = AssessLightingRequirementToolName, ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Asks the Caesarea Security Operations Agent whether an active security operation requires an area to remain lit. Returns a decision and a non-sensitive justification; operational detail is restricted and is never disclosed.")]
    public async Task<SecurityLightingAssessment> AssessLightingRequirementAsync(
        [Description("The area to assess, for example North Promenade.")] string area,
        CancellationToken cancellationToken)
    {
        var correlationId = httpContextAccessor.HttpContext?.Request.Headers[CorrelationHeaderNames.XCorrelationId].FirstOrDefault()
            is { Length: > 0 } header
                ? header
                : CorrelationIds.Create();

        return await assessmentAgent.AssessAsync(area, correlationId, cancellationToken);
    }
}
