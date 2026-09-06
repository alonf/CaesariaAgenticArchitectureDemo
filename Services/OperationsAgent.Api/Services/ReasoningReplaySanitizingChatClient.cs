using Microsoft.Extensions.AI;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Removes reasoning content from messages on their way to the model, so that a tool-calling turn's
/// follow-up request never replays a reasoning item.
/// </summary>
/// <remarks>
/// Azure Foundry's Responses endpoint rejects a replayed reasoning item's <c>encrypted_content</c>
/// with HTTP 400 <c>invalid_payload</c>, and the hosted runtime (store:false) replays each leg's
/// items in the follow-up request - so without this filter every tool-calling turn fails there. The
/// endpoint does not require a function_call to be preceded by its reasoning item, so dropping the
/// item is safe; the cost is that the model re-reasons after each tool result. The diagnosis is
/// recorded in docs/product-status/hosted-agent.md.
///
/// Lives in this project so the deterministic tests can pin it; the hosted head wires it beneath the
/// function-invocation loop (ChatClientAgentOptions.ChatClientFactory), where it sees the loop's
/// replayed messages. WorkforceAgent.Api carries its own copy on purpose - the workforce domain does
/// not reference this one.
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
