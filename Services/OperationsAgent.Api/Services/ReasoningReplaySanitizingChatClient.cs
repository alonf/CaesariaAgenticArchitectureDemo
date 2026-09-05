using Microsoft.Extensions.AI;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Removes reasoning content from messages on their way to the model, so that a tool-calling turn's
/// follow-up request never replays a reasoning item.
/// </summary>
/// <remarks>
/// This works around a defect in the hosted runtime's request composition, diagnosed on 2026-09-05 by
/// capturing the rejected request body (see OperationsAgent.Hosted's ModelTrafficDumpPolicy and
/// docs/product-status/hosted-agent.md):
///
///   - Foundry.Hosting drives the model with store:false and include:["reasoning.encrypted_content"],
///     so a reasoning model returns its chain-of-thought as an opaque encrypted_content blob.
///   - After any function call, the function-invocation loop replays that reasoning item - with the
///     blob - in the follow-up request, because with store:false there is no server-side state to
///     chain to.
///   - Azure Foundry's /openai/v1/responses rejects encrypted_content on INPUT reasoning items with
///     HTTP 400 invalid_payload naming no parameter. The service refuses the very field the SDK asked
///     it to emit.
///
/// The consequence before this filter: every turn in which the model called ANY tool failed, and
/// turns without tool calls succeeded - which masqueraded as an intermittent fault when it was really
/// a deterministic one gated on the model's tool choice.
///
/// Removing the reasoning items is safe against the same endpoint: replaying the captured request
/// without them returns 200 (the endpoint does not require a function_call to be preceded by its
/// reasoning item, unlike openai.com). The cost is that the model re-reasons after each tool result
/// rather than resuming its chain-of-thought - invisible in answers, slightly more reasoning tokens.
///
/// Lives in this project rather than in OperationsAgent.Hosted so the deterministic tests can pin
/// it; the hosted head wires it beneath the function-invocation loop (via
/// ChatClientAgentOptions.ChatClientFactory), where it sees the loop's replayed messages, not just
/// the caller's. WorkforceAgent.Api carries its own copy on purpose - the workforce domain does not
/// reference this one, and a shared cross-domain utility project is a worse trade than one
/// duplicated, pinned file.
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
