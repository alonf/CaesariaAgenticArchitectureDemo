using System.ComponentModel;
using System.Text.Json;
using Azure.AI.Projects;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace SecurityAgent.Api.Services;

/// <summary>
/// The Caesarea Security Operations Agent. Its whole responsibility is to answer one question
/// asked from another domain - "does an active operation require this area to stay lit?" - by
/// reasoning over records only this service can read, and to answer it without disclosing them.
/// </summary>
public sealed partial class SecurityAssessmentAgent(
    AIProjectClient projectClient,
    ISecurityHubGateway securityHub,
    string modelDeploymentName,
    string agentName,
    TimeSpan requestTimeout,
    ILoggerFactory loggerFactory,
    ILogger<SecurityAssessmentAgent> logger)
{
    private const string Instructions = """
        You are the Caesarea Security Operations Agent.
        Another city domain asks you exactly one thing: whether an active security operation
        requires an area to remain lit. Read the operations with the area security tool, which
        serves only the area you were asked about.
        Then classify the situation and decide what the asking domain should do. Several
        operations may overlap, with different windows and notes; use your judgment. Reply with a
        single JSON object and nothing else:
        {"reasonCode": "<one of: NoActiveOperation, ActiveOperationRequiresLighting,
        ActiveOperationWithoutLightingRequirement>",
         "recommendation": "<one of: NoActionRequired, LeaveLitUntilWindowEnds,
        ReassessAfterWindow, ContactSecurityDesk>"}
        Choose ContactSecurityDesk when the records are ambiguous, conflicting, or end very soon
        and a human should decide. Do not write any other text. The asking domain is not cleared
        for operational detail, and only these two choices cross the boundary - unit call signs,
        authorizing officers, classifications, operation identifiers and notes never leave this
        service.
        """;

    private readonly AIProjectClient _projectClient = projectClient ?? throw new ArgumentNullException(nameof(projectClient));
    private readonly ISecurityHubGateway _securityHub = securityHub ?? throw new ArgumentNullException(nameof(securityHub));
    private readonly string _modelDeploymentName = modelDeploymentName;
    private readonly string _agentName = agentName;
    private readonly TimeSpan _requestTimeout = requestTimeout;
    private readonly ILoggerFactory _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    private readonly ILogger<SecurityAssessmentAgent> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Assesses whether an active security operation requires lighting in an area.
    /// </summary>
    /// <param name="area">The area to assess.</param>
    /// <param name="correlationId">The correlation identifier spanning the request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The sanitized assessment that may cross the domain boundary.</returns>
    public async Task<SecurityLightingAssessment> AssessAsync(string area, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(area);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(System.Diagnostics.Debugger.IsAttached ? Timeout.InfiniteTimeSpan : _requestTimeout);

        // One read, one snapshot. The agent reasons over exactly the records the result is
        // computed from, so nothing can be disclosed that was not also checked.
        var status = await _securityHub.GetAreaStatusAsync(area, correlationId, timeoutSource.Token);
        SecurityAgentLog.AssessmentStarted(_logger, area, status.Operations.Count, correlationId);

        #region MULTI_AGENT
        DemoBreakpoints.Pause(DemoSnippets.MultiAgent);

        // A second agent exists because Security is a separate reasoning and permission boundary -
        // not because there is a second Hub. It gets its own instructions, its own tool over its
        // own snapshot, and its own model call; the asking agent never sees any of that.
        var securityTools = new SecurityAreaTools(area, status, correlationId, _loggerFactory.CreateLogger<SecurityAreaTools>());

        AIAgent agent = _projectClient.AsAIAgent(
            options: new ChatClientAgentOptions
            {
                Name = _agentName,
                ChatOptions = new()
                {
                    ModelId = _modelDeploymentName,
                    Instructions = Instructions,
                    Tools =
                    [
                        AIFunctionFactory.Create(
                            securityTools.GetAreaSecurityOperations,
                            SecurityAreaTools.AreaOperationsToolName,
                            "Gets the security operations currently active in the area under assessment.")
                    ]
                }
            },
            loggerFactory: _loggerFactory);
        #endregion

        var session = await agent.CreateSessionAsync(timeoutSource.Token);
        var response = await agent.RunAsync(
            $"Does an active security operation require lighting in {area}?", session, cancellationToken: timeoutSource.Token);

        var assessment = SecurityAssessmentSanitizer.Sanitize(area, response.Text, status, _agentName);
        SecurityAgentLog.AssessmentCompleted(_logger, area, assessment.RequiresLighting, assessment.ReasonCode, correlationId);
        return assessment;
    }
}

/// <summary>
/// The Security Agent's own tool over its own snapshot. It is never advertised outside this
/// service, and it serves exactly one area: a model that asks about a different area gets a
/// refusal, not another domain's records.
/// </summary>
public sealed partial class SecurityAreaTools(
    string boundArea,
    SecurityAreaStatus snapshot,
    string correlationId,
    ILogger<SecurityAreaTools> logger)
{
    /// <summary>
    /// The stable name of the Security Agent's restricted read tool.
    /// </summary>
    public const string AreaOperationsToolName = "get_area_security_operations";

    /// <summary>
    /// Gets the security operations active in the area under assessment.
    /// </summary>
    /// <param name="area">The area to query; must be the area under assessment.</param>
    /// <returns>The active operations, including restricted detail.</returns>
    [Description("Gets the security operations currently active in the area under assessment, including restricted operational detail.")]
    public SecurityAreaStatus GetAreaSecurityOperations(
        [Description("The area under assessment.")] string area)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(area);

        if (!string.Equals(area, boundArea, StringComparison.OrdinalIgnoreCase))
        {
            SecurityAgentLog.OutOfScopeAreaRefused(logger, area, boundArea, correlationId);
            throw new ArgumentException(
                $"This assessment covers {boundArea} only. Operations in other areas are out of scope for this request.",
                nameof(area));
        }

        SecurityAgentLog.RestrictedReadInvoked(logger, boundArea, correlationId);
        return snapshot;
    }
}

/// <summary>
/// Turns the agent's classification into the typed result that may cross the boundary. The model
/// selects from a closed set; the decision, the deadline and every word of the public explanation
/// are produced here, from the authoritative records. Nothing the model wrote is forwarded.
/// </summary>
public static class SecurityAssessmentSanitizer
{
    /// <summary>
    /// Builds the sanitized assessment from the agent's classification and the records it read.
    /// </summary>
    /// <param name="area">The area assessed.</param>
    /// <param name="answer">The agent's raw answer, from which only a known reason code is taken.</param>
    /// <param name="status">The authoritative records the agent reasoned over.</param>
    /// <param name="assessedBy">The consulted agent's name, so the consult is visible in a trace.</param>
    /// <returns>The assessment safe to return across the domain boundary.</returns>
    public static SecurityLightingAssessment Sanitize(
        string area, string? answer, SecurityAreaStatus status, string assessedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(area);
        ArgumentNullException.ThrowIfNull(status);
        ArgumentException.ThrowIfNullOrWhiteSpace(assessedBy);

        // The safety floor is deterministic and the model cannot lower it: if a record says
        // lighting is required, it is required, whatever the classification says. The agent's
        // judgment is allowed to describe the situation, never to switch the lights off.
        var lightingOperations = status.Operations.Where(operation => operation.RequiresLighting).ToArray();
        var requiresLighting = lightingOperations.Length > 0;
        var untilUtc = requiresLighting ? lightingOperations.Max(operation => operation.EndsAt) : (DateTimeOffset?)null;

        var reasonCode = ResolveReasonCode(ExtractCode<SecurityLightingReason>(answer, "reasonCode"), requiresLighting, status.Operations.Count > 0);

        // The recommendation is the agent's own: any value from the closed set is honored, because
        // advice cannot switch the lights off. Only an absent or unrecognized value falls back.
        var recommendation = ExtractCode<SecurityLightingRecommendation>(answer, "recommendation")
            ?? DefaultRecommendation(requiresLighting);

        return new SecurityLightingAssessment(
            area,
            requiresLighting,
            untilUtc,
            reasonCode,
            recommendation,
            RenderReason(reasonCode, untilUtc),
            DetailsWithheld: status.Operations.Count > 0,
            assessedBy);
    }

    private static SecurityLightingRecommendation DefaultRecommendation(bool requiresLighting) =>
        requiresLighting
            ? SecurityLightingRecommendation.LeaveLitUntilWindowEnds
            : SecurityLightingRecommendation.NoActionRequired;

    // The classification may only stand when it agrees with the records; a code that contradicts
    // them is replaced by the one the records support.
    private static SecurityLightingReason ResolveReasonCode(
        SecurityLightingReason? claimed, bool requiresLighting, bool hasOperations)
    {
        var supported = (requiresLighting, hasOperations) switch
        {
            (true, _) => SecurityLightingReason.ActiveOperationRequiresLighting,
            (false, true) => SecurityLightingReason.ActiveOperationWithoutLightingRequirement,
            (false, false) => SecurityLightingReason.NoActiveOperation
        };

        return claimed == supported ? claimed.Value : supported;
    }

    private static string RenderReason(SecurityLightingReason reasonCode, DateTimeOffset? untilUtc) => reasonCode switch
    {
        SecurityLightingReason.ActiveOperationRequiresLighting when untilUtc is { } until =>
            $"An active security operation requires this area to remain lit until {until:u}. Operational details are withheld.",
        SecurityLightingReason.ActiveOperationRequiresLighting =>
            "An active security operation requires this area to remain lit. Operational details are withheld.",
        SecurityLightingReason.ActiveOperationWithoutLightingRequirement =>
            "A security operation is active in this area but does not require lighting. Operational details are withheld.",
        _ => "No active security operation affects lighting in this area."
    };

    private static TCode? ExtractCode<TCode>(string? answer, string property)
        where TCode : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return null;
        }

        var start = answer.IndexOf('{');
        var end = answer.LastIndexOf('}');

        if (start < 0 || end <= start)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(answer[start..(end + 1)]);

            if (!document.RootElement.TryGetProperty(property, out var value)
                || value.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return Enum.TryParse<TCode>(value.GetString(), ignoreCase: true, out var parsed)
                ? parsed
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

internal static partial class SecurityAgentLog
{
    [LoggerMessage(
        EventId = 1710,
        Level = LogLevel.Information,
        Message = "Security Agent assessing lighting requirement for {Area} over {OperationCount} active operation(s). CorrelationId: {CorrelationId}.")]
    internal static partial void AssessmentStarted(ILogger logger, string area, int operationCount, string correlationId);

    [LoggerMessage(
        EventId = 1711,
        Level = LogLevel.Information,
        Message = "Security Agent assessed {Area}: requiresLighting={RequiresLighting}, reason={ReasonCode}. CorrelationId: {CorrelationId}.")]
    internal static partial void AssessmentCompleted(ILogger logger, string area, bool requiresLighting, SecurityLightingReason reasonCode, string correlationId);

    [LoggerMessage(
        EventId = 1712,
        Level = LogLevel.Information,
        Message = "Security Agent read restricted operations for {Area}. CorrelationId: {CorrelationId}.")]
    internal static partial void RestrictedReadInvoked(ILogger logger, string area, string correlationId);

    [LoggerMessage(
        EventId = 1713,
        Level = LogLevel.Warning,
        Message = "Security Agent refused a read for {RequestedArea}; this assessment is bound to {BoundArea}. CorrelationId: {CorrelationId}.")]
    internal static partial void OutOfScopeAreaRefused(ILogger logger, string requestedArea, string boundArea, string correlationId);
}
