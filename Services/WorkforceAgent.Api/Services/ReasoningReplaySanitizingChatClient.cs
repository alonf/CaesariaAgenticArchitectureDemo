using Microsoft.Extensions.AI;

namespace WorkforceAgent.Api.Services;

/// <summary>
/// Removes reasoning content from messages on their way to the model, so that a tool-calling turn's
/// follow-up request never replays a reasoning item.
/// </summary>
/// <remarks>
/// The Workforce Agent's counterpart of the Operations Agent's filter. The Foundry hosted runtime
/// replays each leg's items with store:false, and the Responses endpoint rejects a replayed reasoning
/// item's encrypted_content with HTTP 400 invalid_payload, so any tool call would kill the turn - and
/// this agent's instructions prescribe a two-tool chain. Applied unconditionally in
/// <see cref="WorkforceAgentFactory"/> because it is harmless where the defect is absent: under the
/// Aspire habitat the session chains by previous_response_id and there is nothing to remove. The
/// diagnosis is recorded in docs/product-status/hosted-agent.md.
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
                CreatedAt = message.CreatedAt,
                AdditionalProperties = message.AdditionalProperties,
            };
        }
    }
}
