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
        requires an area to remain lit. Use the area security tool to read the current operations.
        Answer with a single JSON object and nothing else:
        {"requiresLighting": true|false, "untilUtc": "<ISO-8601 or null>", "reason": "<one short sentence>"}
        The reason must be non-sensitive. Never include unit call signs, the authorizing officer,
        the classification, operation identifiers, or operational notes - the asking domain is not
        cleared for them. Say only that an operation requires lighting and until when.
        If no active operation requires lighting, say so plainly.
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

        // The restricted records are read once, here, and never leave this method: the agent
        // reasons over them, and only the sanitized verdict is returned.
        var status = await _securityHub.GetAreaStatusAsync(area, correlationId, timeoutSource.Token);
        SecurityAgentLog.AssessmentStarted(_logger, area, status.Operations.Count, correlationId);

        #region MULTI_AGENT
        DemoBreakpoints.Pause(DemoSnippets.MultiAgent);

        // A second agent exists because Security is a separate reasoning and permission boundary -
        // not because there is a second Hub. It gets its own instructions, its own tool, and its
        // own model call; the asking agent never sees any of that.
        var securityTools = new SecurityAreaTools(_securityHub, correlationId, _loggerFactory.CreateLogger<SecurityAreaTools>());

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
                            securityTools.GetAreaSecurityOperationsAsync,
                            SecurityAreaTools.AreaOperationsToolName,
                            "Gets the security operations currently active in an area.")
                    ]
                }
            },
            loggerFactory: _loggerFactory);
        #endregion

        var session = await agent.CreateSessionAsync(timeoutSource.Token);
        var response = await agent.RunAsync(
            $"Does an active security operation require lighting in {area}?", session, cancellationToken: timeoutSource.Token);

        var assessment = SecurityAssessmentSanitizer.Sanitize(area, response.Text, status);
        SecurityAgentLog.AssessmentCompleted(_logger, area, assessment.RequiresLighting, correlationId);
        return assessment;
    }
}

/// <summary>
/// The Security Agent's own tool over its own Hub. It is never advertised outside this service.
/// </summary>
public sealed partial class SecurityAreaTools(
    ISecurityHubGateway securityHub,
    string correlationId,
    ILogger<SecurityAreaTools> logger)
{
    /// <summary>
    /// The stable name of the Security Agent's restricted read tool.
    /// </summary>
    public const string AreaOperationsToolName = "get_area_security_operations";

    /// <summary>
    /// Gets the security operations currently active in an area.
    /// </summary>
    /// <param name="area">The area to query.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The active operations, including restricted detail.</returns>
    [Description("Gets the security operations currently active in an area, including restricted operational detail.")]
    public async Task<SecurityAreaStatus> GetAreaSecurityOperationsAsync(
        [Description("The area to query, for example North Promenade.")] string area,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(area);
        SecurityAgentLog.RestrictedReadInvoked(logger, area, correlationId);
        return await securityHub.GetAreaStatusAsync(area, correlationId, cancellationToken);
    }
}

/// <summary>
/// Turns the agent's answer into the typed, sanitized result that may cross the boundary. The
/// model is instructed to withhold restricted detail; this makes withholding structural, because
/// an instruction is a request and a boundary should be a guarantee.
/// </summary>
public static class SecurityAssessmentSanitizer
{
    /// <summary>
    /// Builds the sanitized assessment from the agent's answer and the records it reasoned over.
    /// </summary>
    /// <param name="area">The area assessed.</param>
    /// <param name="answer">The agent's raw answer.</param>
    /// <param name="status">The restricted records the agent read.</param>
    /// <returns>The assessment safe to return across the domain boundary.</returns>
    public static SecurityLightingAssessment Sanitize(string area, string? answer, SecurityAreaStatus status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(area);
        ArgumentNullException.ThrowIfNull(status);

        var lightingOperations = status.Operations.Where(operation => operation.RequiresLighting).ToArray();
        var requiresLighting = lightingOperations.Length > 0;
        var untilUtc = requiresLighting ? lightingOperations.Max(operation => operation.EndsAt) : (DateTimeOffset?)null;
        var reason = ExtractReason(answer);

        // Defense in depth: whatever the model wrote, a reason that contains a restricted value is
        // replaced wholesale rather than edited, so no fragment can survive.
        if (reason is null || ContainsRestrictedDetail(reason, status.Operations))
        {
            reason = requiresLighting
                ? "An active security operation requires lighting in this area."
                : "No active security operation requires lighting in this area.";
        }

        return new SecurityLightingAssessment(area, requiresLighting, untilUtc, reason, DetailsWithheld: status.Operations.Count > 0);
    }

    private static bool ContainsRestrictedDetail(string reason, IReadOnlyList<SecurityOperationRecord> operations) =>
        operations.Any(operation =>
            Mentions(reason, operation.OperationId)
            || Mentions(reason, operation.UnitCallSign)
            || Mentions(reason, operation.AuthorizedBy)
            || Mentions(reason, operation.Classification)
            || Mentions(reason, operation.Notes));

    private static bool Mentions(string reason, string restricted) =>
        !string.IsNullOrWhiteSpace(restricted)
        && reason.Contains(restricted, StringComparison.OrdinalIgnoreCase);

    private static string? ExtractReason(string? answer)
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
            return document.RootElement.TryGetProperty("reason", out var reason) ? reason.GetString() : null;
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
        Message = "Security Agent assessed {Area}: requiresLighting={RequiresLighting}. CorrelationId: {CorrelationId}.")]
    internal static partial void AssessmentCompleted(ILogger logger, string area, bool requiresLighting, string correlationId);

    [LoggerMessage(
        EventId = 1712,
        Level = LogLevel.Information,
        Message = "Security Agent read restricted operations for {Area}. CorrelationId: {CorrelationId}.")]
    internal static partial void RestrictedReadInvoked(ILogger logger, string area, string correlationId);
}
