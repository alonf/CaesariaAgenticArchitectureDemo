using Microsoft.Extensions.AI;

namespace WorkforceAgent.Api.Services;

/// <summary>
/// Removes reasoning content from messages on their way to the model, so that a tool-calling turn's
/// follow-up request never replays a reasoning item.
/// </summary>
/// <remarks>
/// The Workforce Agent's counterpart of <c>OperationsAgent.Hosted.ReasoningReplaySanitizingChatClient</c>,
/// where the defect was diagnosed; the full story is in docs/product-status/hosted-agent.md. In
/// short: the Foundry hosted runtime drives the model with store:false and replays each leg's items
/// in the follow-up request, and Azure Foundry's Responses endpoint rejects a replayed reasoning
/// item's encrypted_content with HTTP 400 invalid_payload. Any tool call therefore kills the turn.
///
/// This agent is the worst possible victim: its instructions prescribe a two-tool chain - find the
/// work orders, then open one - so hosted, it would fail nearly every time.
///
/// Applied unconditionally in <see cref="WorkforceAgentFactory"/> rather than only in the hosted
/// head, because it is harmless where the defect is absent: under the Aspire habitat the session
/// chains by previous_response_id, no reasoning items are replayed, and this filter has nothing to
/// remove. The cost where it does act is that the model re-reasons after each tool result rather
/// than resuming its chain-of-thought.
/// </remarks>
internal sealed class ReasoningReplaySanitizingChatClient(IChatClient innerClient) : DelegatingChatClient(innerClient)
{
    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => base.GetResponseAsync(Sanitize(messages), options, cancellationToken);

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => base.GetStreamingResponseAsync(Sanitize(messages), options, cancellationToken);

    private static IEnumerable<ChatMessage> Sanitize(IEnumerable<ChatMessage> messages)
    {
        foreach (var message in messages)
        {
            if (!message.Contents.Any(content => content is TextReasoningContent))
            {
                yield return message;
                continue;
            }

            var kept = message.Contents.Where(content => content is not TextReasoningContent).ToList();

            // A message that carried only reasoning has nothing left to say; sending an empty
            // assistant message is its own way to earn a schema rejection.
            if (kept.Count == 0)
            {
                continue;
            }

            // A rebuilt message, not a mutated one: the original list belongs to the
            // function-invocation loop's own state, which will be consulted again on the next leg.
            // RawRepresentation is deliberately not carried over - if the serializer preferred it,
            // the reasoning item would ride back in through the raw form of the whole message.
            yield return new ChatMessage(message.Role, kept)
            {
                AuthorName = message.AuthorName,
                MessageId = message.MessageId,
                AdditionalProperties = message.AdditionalProperties,
            };
        }
    }
}
