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
    /// forwarding tool-result text, so only fields allowed to cross can appear. A consultation
    /// that produced no judgment still gets a row - without a verdict - because an answer given
    /// without the specialist must not read like an answer that never asked.
    /// </summary>
    /// <param name="recorder">The run's flight recorder.</param>
    /// <param name="approvalDecisions">The operator's decision per intercepted call identifier.</param>
    /// <param name="specialistName">The consulted agent's name, for a consultation that returned nothing to name itself with.</param>
    /// <param name="onUnreadable">Reports a consulted agent whose answer did not match its contract.</param>
    /// <returns>One entry per consultation, carrying the judgment when one crossed.</returns>
    public static IReadOnlyList<OperationsAgentDelegation> DescribeDelegations(
        ModelExchangeRecorder recorder,
        IReadOnlyDictionary<string, bool> approvalDecisions,
        string specialistName,
        Action<string, Exception>? onUnreadable = null)
    {
        ArgumentNullException.ThrowIfNull(recorder);

        List<OperationsAgentDelegation> delegations = [];

        foreach (var call in recorder.ToolCalls.Where(call =>
            string.Equals(call.ToolName, OperationsAgentToolNames.AssessLightingRequirement, StringComparison.Ordinal)))
        {
            var status = ResolveStatus(call, recorder, approvalDecisions);

            if (status != OperationsAgentToolCallStatus.Completed
                || recorder.FindResult(call.CallId)?.Text is not { } payload)
            {
                delegations.Add(WithoutJudgment(call, specialistName, status));
                continue;
            }

            ConsultedSecurityAssessment? assessment;

            try
            {
                assessment = JsonSerializer.Deserialize<ConsultedSecurityAssessment>(payload, SerializerOptions);
            }
            catch (JsonException exception)
            {
                // A specialist that answered in an unexpected shape is not described from text
                // nobody validated: the row says it was asked and that no judgment came back.
                onUnreadable?.Invoke(call.ToolName, exception);
                delegations.Add(WithoutJudgment(call, specialistName, OperationsAgentToolCallStatus.Failed));
                continue;
            }

            if (assessment is not { AssessedBy: { Length: > 0 } assessedBy, Area: { Length: > 0 } area })
            {
                delegations.Add(WithoutJudgment(call, specialistName, OperationsAgentToolCallStatus.Failed));
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

    private static OperationsAgentDelegation WithoutJudgment(
        RecordedToolCall call, string specialistName, OperationsAgentToolCallStatus status) =>
        new(
            call.ToolName,
            specialistName,
            AreaAskedAbout(call.Arguments),
            RequiresLighting: false,
            UntilUtc: null,
            ReasonCode: string.Empty,
            Recommendation: string.Empty,
            Reason: string.Empty,
            DetailsWithheld: false,
            status);

    // The specialist never said which area it judged, so the row names the one the model asked about.
    private static string AreaAskedAbout(string arguments)
    {
        try
        {
            using var document = JsonDocument.Parse(arguments);

            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("area", out var area)
                && area.ValueKind == JsonValueKind.String
                ? area.GetString() ?? string.Empty
                : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : $"{value[..maxLength]}…";
}
