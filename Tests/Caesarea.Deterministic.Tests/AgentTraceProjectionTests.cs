using Microsoft.Extensions.AI;
using OperationsAgent.Api.Services;
using OperationsAgent.Contracts;

namespace Caesarea.Deterministic.Tests;

/// <summary>
/// The trace the operator reads makes two claims: which capabilities actually ran, and what
/// another domain's agent answered. Both are exercised here through the real recorder, driven by a
/// deterministic chat client - the approval stages teach that a requested call is not an executed
/// one, so a trace that cannot tell them apart teaches the opposite.
/// </summary>
public sealed class AgentTraceProjectionTests
{
    private const string ProtectedTool = "create_maintenance_work_item";

    [Fact]
    public async Task ADeclinedCapabilityIsNeverReportedAsExecuted()
    {
        // The framework answers a declined call with a rejection result, so a result alone would
        // mark it Completed. The operator's decision is what settles it.
        var recorder = await RecordAsync(
            [Call("call-1", ProtectedTool)],
            [Result("call-1", "Tool call invocation rejected.")]);

        var calls = AgentTraceProjection.DescribeToolCalls(
            recorder, new Dictionary<string, bool> { ["call-1"] = false });

        Assert.Equal(OperationsAgentToolCallStatus.Denied, Assert.Single(calls).Status);
    }

    [Fact]
    public async Task AnApprovedCapabilityThatRanIsReportedAsCompleted()
    {
        var recorder = await RecordAsync(
            [Call("call-1", ProtectedTool)],
            [Result("call-1", "Maintenance work item WI-1 filed for L-417.")]);

        var calls = AgentTraceProjection.DescribeToolCalls(
            recorder, new Dictionary<string, bool> { ["call-1"] = true });

        Assert.Equal(OperationsAgentToolCallStatus.Completed, Assert.Single(calls).Status);
    }

    [Fact]
    public async Task ACallWithNoOutcomeIsReportedAsRequested()
    {
        // The run ended before the pipeline fed a result back: honest is "asked for, not run".
        var recorder = await RecordAsync([Call("call-1", "get_streetlight_state")], []);

        var calls = AgentTraceProjection.DescribeToolCalls(recorder, new Dictionary<string, bool>());

        Assert.Equal(OperationsAgentToolCallStatus.Requested, Assert.Single(calls).Status);
    }

    [Fact]
    public async Task ACallThatThrewIsReportedAsFailed()
    {
        var recorder = await RecordAsync(
            [Call("call-1", "get_streetlight_state")],
            [new FunctionResultContent("call-1", "boom") { Exception = new InvalidOperationException("boom") }]);

        var calls = AgentTraceProjection.DescribeToolCalls(recorder, new Dictionary<string, bool>());

        Assert.Equal(OperationsAgentToolCallStatus.Failed, Assert.Single(calls).Status);
    }

    [Fact]
    public async Task ARepeatedRefusalKeepsBothRequestsInTheTrace()
    {
        // The model asked twice and was refused twice. Collapsing that would hide a re-request.
        var recorder = await RecordAsync(
            [Call("call-1", ProtectedTool), Call("call-2", ProtectedTool)],
            [Result("call-1", "Tool call invocation rejected."), Result("call-2", "Tool call invocation rejected.")]);

        var calls = AgentTraceProjection.DescribeToolCalls(
            recorder, new Dictionary<string, bool> { ["call-1"] = false, ["call-2"] = false });

        Assert.Equal(2, calls.Count);
        Assert.All(calls, call => Assert.Equal(OperationsAgentToolCallStatus.Denied, call.Status));
    }

    [Fact]
    public async Task AConsultedSpecialistAppearsAsItsOwnTypedStep()
    {
        // The judgment the specialist published, read from its own contract rather than forwarded
        // as tool-result text - so only fields that are allowed to cross can appear.
        var payload = """
            {"area":"North Promenade","requiresLighting":true,
             "untilUtc":"2026-09-02T00:42:57.9799981+00:00",
             "reasonCode":"ActiveOperationRequiresLighting",
             "recommendation":"LeaveLitUntilWindowEnds",
             "reason":"An active security operation requires this area to remain lit until 2026-09-02 00:42:57Z. Operational details are withheld.",
             "detailsWithheld":true,"assessedBy":"Caesarea Security Operations Agent"}
            """;
        var recorder = await RecordAsync(
            [Call("call-1", OperationsAgentToolNames.AssessLightingRequirement)],
            [Result("call-1", payload)]);

        var delegation = Assert.Single(
            AgentTraceProjection.DescribeDelegations(recorder, new Dictionary<string, bool>()));

        Assert.Equal("Caesarea Security Operations Agent", delegation.AssessedBy);
        Assert.Equal("North Promenade", delegation.Area);
        Assert.True(delegation.RequiresLighting);
        Assert.Equal("ActiveOperationRequiresLighting", delegation.ReasonCode);
        Assert.Equal("LeaveLitUntilWindowEnds", delegation.Recommendation);
        Assert.True(delegation.DetailsWithheld);
        Assert.NotNull(delegation.UntilUtc);
    }

    [Fact]
    public async Task AConsultationThatWasNotCompletedContributesNoJudgment()
    {
        var recorder = await RecordAsync(
            [Call("call-1", OperationsAgentToolNames.AssessLightingRequirement)], []);

        Assert.Empty(AgentTraceProjection.DescribeDelegations(recorder, new Dictionary<string, bool>()));
    }

    [Fact]
    public async Task AnAnswerThatDoesNotMatchTheContractIsReportedAndOmitted()
    {
        var recorder = await RecordAsync(
            [Call("call-1", OperationsAgentToolNames.AssessLightingRequirement)],
            [Result("call-1", "The unit on watch is NIGHTHAWK-3.")]);

        List<string> unreadable = [];
        var delegations = AgentTraceProjection.DescribeDelegations(
            recorder, new Dictionary<string, bool>(), (toolName, _) => unreadable.Add(toolName));

        // Nothing invented from prose, and the operator is not silently shown a shorter trace.
        Assert.Empty(delegations);
        Assert.Equal(OperationsAgentToolNames.AssessLightingRequirement, Assert.Single(unreadable));
    }

    [Fact]
    public async Task OnlyTheConsultToolProducesADelegationRow()
    {
        var recorder = await RecordAsync(
            [Call("call-1", "get_streetlight_state")],
            [Result("call-1", """{"assetId":"L-417","assessedBy":"not a specialist","area":"North Promenade"}""")]);

        Assert.Empty(AgentTraceProjection.DescribeDelegations(recorder, new Dictionary<string, bool>()));
    }

    private static FunctionCallContent Call(string callId, string toolName) =>
        new(callId, toolName, new Dictionary<string, object?> { ["assetId"] = "L-417" });

    private static FunctionResultContent Result(string callId, string result) => new(callId, result);

    // Drives the real recorder the way the pipeline does: the model returns calls on one round
    // trip, and their results arrive in the request messages of the next.
    private static async Task<ModelExchangeRecorder> RecordAsync(
        IReadOnlyList<FunctionCallContent> calls, IReadOnlyList<FunctionResultContent> results)
    {
        var recorder = new ModelExchangeRecorder(new ScriptedChatClient(calls));
        var cancellationToken = TestContext.Current.CancellationToken;

        await recorder.GetResponseAsync([new ChatMessage(ChatRole.User, "Why is L-417 on?")], cancellationToken: cancellationToken);

        if (results.Count > 0)
        {
            await recorder.GetResponseAsync(
                [new ChatMessage(ChatRole.Tool, [.. results])], cancellationToken: cancellationToken);
        }

        return recorder;
    }

    private sealed class ScriptedChatClient(IReadOnlyList<FunctionCallContent> calls) : IChatClient
    {
        private int _round;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(_round++ == 0
                ? new ChatMessage(ChatRole.Assistant, [.. calls])
                : new ChatMessage(ChatRole.Assistant, "Done.")));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
            // Nothing to release.
        }
    }
}
