using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Debugger-friendly flight recorder for the model pipeline: captures every round trip to the model as
/// readable strings, so a presenter paused at a demo breakpoint can inspect exactly what was sent to the
/// model (instructions, question, tool results) and what came back (tool calls, the final answer).
/// </summary>
[DebuggerDisplay("{Exchanges.Count} model round trip(s)")]
public sealed class ModelExchangeRecorder(IChatClient innerClient) : DelegatingChatClient(innerClient)
{
    /// <summary>
    /// Gets the recorded model round trips, in order.
    /// </summary>
    public IReadOnlyList<ModelExchange> Exchanges => _exchanges;

    /// <summary>
    /// Gets the tool invocations the model requested, in order.
    /// </summary>
    public IReadOnlyList<RecordedToolCall> ToolCalls => _toolCalls;

    private readonly List<ModelExchange> _exchanges = [];
    private readonly List<RecordedToolCall> _toolCalls = [];

    /// <inheritdoc />
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var messageList = messages as IReadOnlyList<ChatMessage> ?? [.. messages];
        var sent = messageList.Select(Describe).ToList();

        var response = await base.GetResponseAsync(messageList, options, cancellationToken);

        foreach (var call in response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>())
        {
            _toolCalls.Add(new RecordedToolCall(call.Name, DescribeArguments(call.Arguments)));
        }

        _exchanges.Add(new ModelExchange(_exchanges.Count + 1, sent, [.. response.Messages.Select(Describe)]));
        return response;
    }

    private static string Describe(ChatMessage message)
    {
        var parts = message.Contents.Select(content => content switch
        {
            TextContent text => text.Text,
            FunctionCallContent call => $"[tool call] {call.Name}({DescribeArguments(call.Arguments)})",
            FunctionResultContent result => $"[tool result] {result.Result}",
            _ => $"[{content.GetType().Name}]"
        });

        return $"{message.Role}: {string.Join(" | ", parts)}";
    }

    private static string DescribeArguments(IDictionary<string, object?>? arguments) =>
        arguments is null ? string.Empty : JsonSerializer.Serialize(arguments);
}

/// <summary>
/// Represents one tool invocation the model requested during a run.
/// </summary>
/// <param name="ToolName">The stable tool name the model invoked.</param>
/// <param name="Arguments">The tool arguments as compact JSON.</param>
[DebuggerDisplay("{ToolName,nq}({Arguments,nq})")]
public sealed record RecordedToolCall(string ToolName, string Arguments);

/// <summary>
/// Represents one recorded model round trip: what was sent and what the model returned.
/// </summary>
/// <param name="RoundTrip">The 1-based round-trip number within the current agent run.</param>
/// <param name="Sent">The messages sent to the model, one readable string per message.</param>
/// <param name="Received">The messages the model returned, one readable string per message.</param>
[DebuggerDisplay("{Summary,nq}")]
public sealed record ModelExchange(int RoundTrip, IReadOnlyList<string> Sent, IReadOnlyList<string> Received)
{
    /// <summary>
    /// Gets a one-line summary shown by the debugger for this round trip.
    /// </summary>
    public string Summary => $"#{RoundTrip}: sent {Sent.Count} message(s) -> {(Received.Count > 0 ? Received[^1] : "(no reply)")}";
}
