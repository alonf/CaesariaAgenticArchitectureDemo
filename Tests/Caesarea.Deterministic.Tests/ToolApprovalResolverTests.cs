using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OperationsAgent.Api.Services;

namespace Caesarea.Deterministic.Tests;

/// <summary>
/// The control flow that decides whether a protected capability runs, exercised without a model.
/// A supervisor's answer only means something if it is carried faithfully: an approval resumes the
/// run, a refusal stands for the rest of the request, and a model that will not stop asking is an
/// explicit failure rather than a half-finished answer.
/// </summary>
public sealed class ToolApprovalResolverTests
{
    private const string ToolName = "create_maintenance_work_item";

    [Fact]
    public async Task AResponseWithNoRequestsIsReturnedUntouched()
    {
        var resolver = new ToolApprovalResolver(3);
        var answer = new AgentResponse(new ChatMessage(ChatRole.Assistant, "Nothing needed approval."));

        var resolved = await resolver.ResolveAsync(
            answer,
            (_, _) => throw new InvalidOperationException("The operator must not be asked."),
            (_, _) => throw new InvalidOperationException("The run must not be resumed."),
            onStandingRefusal: null,
            TestContext.Current.CancellationToken);

        Assert.Same(answer, resolved);
    }

    [Fact]
    public async Task ADecisionIsCarriedBackAndTheRunResumes()
    {
        var resolver = new ToolApprovalResolver(3);
        List<ToolApprovalResponseContent> carried = [];

        var resolved = await resolver.ResolveAsync(
            CreateRequestingResponse("req-1"),
            (_, _) => Task.FromResult(true),
            (message, _) =>
            {
                carried.AddRange(message.Contents.OfType<ToolApprovalResponseContent>());
                return Task.FromResult(new AgentResponse(new ChatMessage(ChatRole.Assistant, "Work item WI-1 filed.")));
            },
            onStandingRefusal: null,
            TestContext.Current.CancellationToken);

        Assert.Equal("Work item WI-1 filed.", resolved.Text);
        var decision = Assert.Single(carried);
        Assert.True(decision.Approved);
    }

    [Fact]
    public async Task ARefusalStandsForTheRestOfTheRequest()
    {
        // The model may re-request a capability the operator just declined. Asking again would
        // spend the round budget on a question already answered, so the standing decision answers
        // it - and says so, because a decision applied silently is a decision nobody can audit.
        var resolver = new ToolApprovalResolver(4);
        var asked = 0;
        List<string> standingRefusals = [];
        var round = 0;

        await Assert.ThrowsAsync<OperationsAgentApprovalLoopException>(() => resolver.ResolveAsync(
            CreateRequestingResponse("req-1"),
            (_, _) =>
            {
                asked++;
                return Task.FromResult(false);
            },
            (_, _) => Task.FromResult(CreateRequestingResponse($"req-{++round + 1}")),
            standingRefusals.Add,
            TestContext.Current.CancellationToken));

        // Asked exactly once; every later round was answered from the standing refusal.
        Assert.Equal(1, asked);
        Assert.Equal(3, standingRefusals.Count);
        Assert.All(standingRefusals, name => Assert.Equal(ToolName, name));
    }

    [Fact]
    public async Task AModelThatNeverStopsAskingFailsTheRequest()
    {
        // Bounded resolution: the alternative is returning an answer whose tool calls never ran,
        // which reads on stage exactly like an answer whose tool calls did.
        var resolver = new ToolApprovalResolver(2);
        var resumes = 0;

        var exception = await Assert.ThrowsAsync<OperationsAgentApprovalLoopException>(() => resolver.ResolveAsync(
            CreateRequestingResponse("req-1"),
            (_, _) => Task.FromResult(true),
            (_, _) =>
            {
                resumes++;
                return Task.FromResult(CreateRequestingResponse($"req-{resumes + 1}"));
            },
            onStandingRefusal: null,
            TestContext.Current.CancellationToken));

        Assert.Equal(2, resumes);
        Assert.Contains(ToolName, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheOperatorIsToldWhichCapabilityAndWhichArguments()
    {
        var request = new ToolApprovalRequestContent(
            "req-1",
            new FunctionCallContent("call-1", ToolName, new Dictionary<string, object?>
            {
                ["assetId"] = "L-417",
                ["summary"] = "Controller unresponsive."
            }));

        var (name, arguments) = ToolApprovalResolver.DescribeToolCall(request);

        Assert.Equal(ToolName, name);
        Assert.Contains("assetId: L-417", arguments, StringComparison.Ordinal);
        Assert.Contains("summary: Controller unresponsive.", arguments, StringComparison.Ordinal);
    }

    [Fact]
    public void ACallWithoutArgumentsIsDescribedHonestly()
    {
        var request = new ToolApprovalRequestContent("req-1", new FunctionCallContent("call-1", ToolName, null));

        var (name, arguments) = ToolApprovalResolver.DescribeToolCall(request);

        Assert.Equal(ToolName, name);
        Assert.Equal("no arguments", arguments);
    }

    private static AgentResponse CreateRequestingResponse(string requestId) =>
        new(new ChatMessage(ChatRole.Assistant, [
            new ToolApprovalRequestContent(
                requestId,
                new FunctionCallContent($"call-{requestId}", ToolName, new Dictionary<string, object?>
                {
                    ["assetId"] = "L-417"
                }))
        ]));
}
