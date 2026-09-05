using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Caesarea.Deterministic.Tests;

/// <summary>
/// The sanitizer rewrites what the model is sent on every tool-calling turn, in both hosted
/// agents, so its behaviour is pinned here against a scripted client rather than trusted. Two
/// copies exist on purpose - OperationsAgent.Api's (wired by the hosted head) and
/// WorkforceAgent.Api's (wired by its factory) - because the domains do not reference each other;
/// every test therefore runs against both, so the copies cannot drift apart silently. The defect
/// they work around: the hosted runtime replays a reasoning item's encrypted_content and the
/// service answers HTTP 400 invalid_payload (see docs/product-status/hosted-agent.md).
/// </summary>
public sealed class ReasoningReplaySanitizingChatClientTests
{
    public static TheoryData<string> Sanitizers => new(OperationsSanitizer, WorkforceSanitizer);

    private const string OperationsSanitizer = "operations";
    private const string WorkforceSanitizer = "workforce";

    private static IChatClient CreateSanitizer(string sanitizer, IChatClient inner) => sanitizer switch
    {
        OperationsSanitizer => new OperationsAgent.Api.Services.ReasoningReplaySanitizingChatClient(inner),
        WorkforceSanitizer => new WorkforceAgent.Api.Services.ReasoningReplaySanitizingChatClient(inner),
        _ => throw new ArgumentOutOfRangeException(nameof(sanitizer), sanitizer, "Unknown sanitizer under test.")
    };

    [Theory]
    [MemberData(nameof(Sanitizers))]
    public async Task ABufferedCallStripsReasoningAndForwardsEverythingElseUntouched(string sanitizer)
    {
        var inner = new CapturingChatClient();
        using var client = CreateSanitizer(sanitizer, inner);

        var user = new ChatMessage(ChatRole.User, "Why is L-417 on?");
        var assistant = new ChatMessage(ChatRole.Assistant,
        [
            new TextReasoningContent("private chain-of-thought"),
            new TextContent("Checking the asset now."),
        ]);
        var tool = new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call-1", "lamp is on")]);

        var response = await client.GetResponseAsync(
            [user, assistant, tool], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(3, inner.Sent.Count);

        // Messages that carried no reasoning are not rebuilt: the very instances go through.
        Assert.Same(user, inner.Sent[0]);
        Assert.Same(tool, inner.Sent[2]);

        // The assistant message keeps its text and loses only the reasoning.
        var forwarded = Assert.Single(inner.Sent[1].Contents);
        Assert.Equal("Checking the asset now.", Assert.IsType<TextContent>(forwarded).Text);

        // The inner client's answer comes back unwrapped.
        Assert.Same(inner.Response, response);
    }

    [Theory]
    [MemberData(nameof(Sanitizers))]
    public async Task TheStreamingPathStripsOutgoingMessagesTheSameWay(string sanitizer)
    {
        var inner = new CapturingChatClient();
        using var client = CreateSanitizer(sanitizer, inner);

        var user = new ChatMessage(ChatRole.User, "Why is L-417 on?");
        var assistant = new ChatMessage(ChatRole.Assistant,
        [
            new TextReasoningContent("private chain-of-thought"),
            new TextContent("Checking the asset now."),
        ]);

        // Drained so the inner client actually enumerates the sanitized sequence.
        var updates = 0;
        await foreach (var update in client.GetStreamingResponseAsync(
            [user, assistant], cancellationToken: TestContext.Current.CancellationToken))
        {
            updates++;
        }

        Assert.Equal(1, updates);
        Assert.Equal(2, inner.Sent.Count);
        Assert.Same(user, inner.Sent[0]);
        var forwarded = Assert.Single(inner.Sent[1].Contents);
        Assert.Equal("Checking the asset now.", Assert.IsType<TextContent>(forwarded).Text);
    }

    [Theory]
    [MemberData(nameof(Sanitizers))]
    public async Task AMessageMixingReasoningAndAFunctionCallKeepsTheCall(string sanitizer)
    {
        var inner = new CapturingChatClient();
        using var client = CreateSanitizer(sanitizer, inner);

        // The shape the function-invocation loop replays: the model reasoned, then called a tool.
        var call = new FunctionCallContent("call-1", "find_work_orders_for_asset",
            new Dictionary<string, object?> { ["assetId"] = "L-417" });
        var assistant = new ChatMessage(ChatRole.Assistant,
            [new TextReasoningContent("private chain-of-thought"), call]);

        await client.GetResponseAsync([assistant], cancellationToken: TestContext.Current.CancellationToken);

        var forwarded = Assert.Single(inner.Sent);
        Assert.Equal(ChatRole.Assistant, forwarded.Role);

        // The call itself is the same instance, not a copy - the follow-up leg must still
        // correlate it with its function_call_output by id.
        Assert.Same(call, Assert.Single(forwarded.Contents));
    }

    [Theory]
    [MemberData(nameof(Sanitizers))]
    public async Task AReasoningOnlyMessageIsDroppedRatherThanSentEmpty(string sanitizer)
    {
        var inner = new CapturingChatClient();
        using var client = CreateSanitizer(sanitizer, inner);

        var user = new ChatMessage(ChatRole.User, "Why is L-417 on?");
        var reasoningOnly = new ChatMessage(ChatRole.Assistant, [new TextReasoningContent("private chain-of-thought")]);
        var tool = new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call-1", "lamp is on")]);

        await client.GetResponseAsync(
            [user, reasoningOnly, tool], cancellationToken: TestContext.Current.CancellationToken);

        // Dropped entirely: an empty assistant message is its own way to earn a schema rejection.
        Assert.Equal(2, inner.Sent.Count);
        Assert.All(inner.Sent, message => Assert.NotEmpty(message.Contents));
        Assert.DoesNotContain(inner.Sent,
            message => message.Contents.Any(content => content is TextReasoningContent));
    }

    [Theory]
    [MemberData(nameof(Sanitizers))]
    public async Task ARebuiltMessageKeepsItsIdentityAndMetadata(string sanitizer)
    {
        var inner = new CapturingChatClient();
        using var client = CreateSanitizer(sanitizer, inner);

        var createdAt = new DateTimeOffset(2026, 9, 5, 8, 30, 0, TimeSpan.Zero);
        var properties = new AdditionalPropertiesDictionary { ["turn"] = 2 };
        var assistant = new ChatMessage(ChatRole.Assistant,
            [new TextReasoningContent("private chain-of-thought"), new TextContent("Checking now.")])
        {
            AuthorName = "workforce-agent",
            MessageId = "msg-42",
            CreatedAt = createdAt,
            AdditionalProperties = properties,
        };

        await client.GetResponseAsync([assistant], cancellationToken: TestContext.Current.CancellationToken);

        var forwarded = Assert.Single(inner.Sent);
        Assert.Equal(ChatRole.Assistant, forwarded.Role);
        Assert.Equal("workforce-agent", forwarded.AuthorName);
        Assert.Equal("msg-42", forwarded.MessageId);
        Assert.Equal(createdAt, forwarded.CreatedAt);
        Assert.Same(properties, forwarded.AdditionalProperties);
    }

    [Theory]
    [MemberData(nameof(Sanitizers))]
    public async Task TheCallersOriginalMessagesAreNotMutated(string sanitizer)
    {
        var inner = new CapturingChatClient();
        using var client = CreateSanitizer(sanitizer, inner);

        // The function-invocation loop owns these instances and consults them again on the next
        // leg; stripping them in place would corrupt its state.
        var reasoning = new TextReasoningContent("private chain-of-thought");
        var text = new TextContent("Checking now.");
        var assistant = new ChatMessage(ChatRole.Assistant, [reasoning, text]);
        var reasoningOnly = new ChatMessage(ChatRole.Assistant, [new TextReasoningContent("more thought")]);

        await client.GetResponseAsync(
            [assistant, reasoningOnly], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, assistant.Contents.Count);
        Assert.Same(reasoning, assistant.Contents[0]);
        Assert.Same(text, assistant.Contents[1]);
        Assert.Single(reasoningOnly.Contents);
    }

    [Theory]
    [MemberData(nameof(Sanitizers))]
    public async Task TheCancellationTokenReachesTheInnerClient(string sanitizer)
    {
        var inner = new CapturingChatClient();
        using var client = CreateSanitizer(sanitizer, inner);
        using var source = new CancellationTokenSource();
        var messages = new[] { new ChatMessage(ChatRole.User, "Why is L-417 on?") };

        await client.GetResponseAsync(messages, cancellationToken: source.Token);
        Assert.Equal(source.Token, inner.Token);

        // Drained so the iterator body runs and records its token.
        using var streamingSource = new CancellationTokenSource();
        var updates = 0;
        await foreach (var update in client.GetStreamingResponseAsync(messages, cancellationToken: streamingSource.Token))
        {
            updates++;
        }

        Assert.Equal(1, updates);
        Assert.Equal(streamingSource.Token, inner.Token);
    }

    /// <summary>
    /// Records what actually reaches the model's client. The sanitizer yields lazily, so the
    /// capture materialises the sequence exactly the way a real client would.
    /// </summary>
    private sealed class CapturingChatClient : IChatClient
    {
        public List<ChatMessage> Sent { get; private set; } = [];

        public CancellationToken Token { get; private set; }

        public ChatResponse Response { get; } = new(new ChatMessage(ChatRole.Assistant, "Done."));

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Sent = [.. messages];
            Token = cancellationToken;
            return Task.FromResult(Response);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            Sent = [.. messages];
            Token = cancellationToken;
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Done.");
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
            // Nothing to release.
        }
    }
}
