using System.Text.Json;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Turns the run's flight recording into the trace the operator sees. Separated from the agent so
/// the two claims it makes - what actually ran, and what another domain's agent answered - can be
/// exercised without a model.
/// </summary>
public static class AgentTraceProjection
{
    // The specialist renders its public sentence from a template, so this is a projector guard
    // rather than a trust boundary: a delegation row must stay readable on a slide.
    private const int MaxDelegationReasonLength = 400;

    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    /// <summary>
    /// Decides what became of one requested tool call. The operator's decision wins over the
    /// presence of a result, because the framework answers a declined call with a rejection result
    /// too - so "a result came back" is not evidence the capability ran.
    /// </summary>
    /// <param name="call">The recorded call.</param>
    /// <param name="recorder">The run's flight recorder.</param>
    /// <param name="approvalDecisions">The operator's decision per intercepted call identifier.</param>
    /// <returns>The outcome the pipeline actually reached.</returns>
    public static OperationsAgentToolCallStatus ResolveStatus(
        RecordedToolCall call,
        ModelExchangeRecorder recorder,
        IReadOnlyDictionary<string, bool> approvalDecisions)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(recorder);
        ArgumentNullException.ThrowIfNull(approvalDecisions);

        if (approvalDecisions.TryGetValue(call.CallId, out var approved) && !approved)
        {
            return OperationsAgentToolCallStatus.Denied;
        }

        return recorder.FindResult(call.CallId) switch
        {
            null => OperationsAgentToolCallStatus.Requested,
            { Failed: true } => OperationsAgentToolCallStatus.Failed,
            _ => OperationsAgentToolCallStatus.Completed
        };
    }

    /// <summary>
    /// Projects every recorded tool call with the outcome it reached.
    /// </summary>
    /// <param name="recorder">The run's flight recorder.</param>
    /// <param name="approvalDecisions">The operator's decision per intercepted call identifier.</param>
    /// <returns>The calls the model requested, in order, each with its outcome.</returns>
    public static IReadOnlyList<OperationsAgentToolCall> DescribeToolCalls(
        ModelExchangeRecorder recorder, IReadOnlyDictionary<string, bool> approvalDecisions)
    {
        ArgumentNullException.ThrowIfNull(recorder);

        return [.. recorder.ToolCalls.Select(call => new OperationsAgentToolCall(
            call.ToolName, call.Arguments, ResolveStatus(call, recorder, approvalDecisions)))];
    }

    /// <summary>
    /// Builds the visible delegation trace: which specialist agent was consulted and the sanitized
    /// judgment it returned. The specialist's published contract is deserialized rather than
    /// forwarding tool-result text, so only fields allowed to cross can appear.
    /// </summary>
    /// <param name="recorder">The run's flight recorder.</param>
    /// <param name="approvalDecisions">The operator's decision per intercepted call identifier.</param>
    /// <param name="onUnreadable">Reports a consulted agent whose answer did not match its contract.</param>
    /// <returns>One entry per completed consultation.</returns>
    public static IReadOnlyList<OperationsAgentDelegation> DescribeDelegations(
        ModelExchangeRecorder recorder,
        IReadOnlyDictionary<string, bool> approvalDecisions,
        Action<string, Exception>? onUnreadable = null)
    {
        ArgumentNullException.ThrowIfNull(recorder);

        List<OperationsAgentDelegation> delegations = [];

        foreach (var call in recorder.ToolCalls.Where(call =>
            string.Equals(call.ToolName, OperationsAgentToolNames.AssessLightingRequirement, StringComparison.Ordinal)))
        {
            // A consultation that did not complete produced no judgment, so it contributes no row.
            if (ResolveStatus(call, recorder, approvalDecisions) != OperationsAgentToolCallStatus.Completed
                || recorder.FindResult(call.CallId)?.Text is not { } payload)
            {
                continue;
            }

            ConsultedSecurityAssessment? assessment;

            try
            {
                assessment = JsonSerializer.Deserialize<ConsultedSecurityAssessment>(payload, SerializerOptions);
            }
            catch (JsonException exception)
            {
                // A specialist that answered in an unexpected shape is left out of the trace
                // rather than described from text nobody validated.
                onUnreadable?.Invoke(call.ToolName, exception);
                continue;
            }

            if (assessment is not { AssessedBy: { Length: > 0 } assessedBy, Area: { Length: > 0 } area })
            {
                continue;
            }

            delegations.Add(new OperationsAgentDelegation(
                call.ToolName,
                assessedBy,
                area,
                assessment.RequiresLighting,
                assessment.UntilUtc,
                assessment.ReasonCode ?? string.Empty,
                assessment.Recommendation ?? string.Empty,
                Truncate(assessment.Reason ?? string.Empty, MaxDelegationReasonLength),
                assessment.DetailsWithheld));
        }

        return delegations;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : $"{value[..maxLength]}…";
}
