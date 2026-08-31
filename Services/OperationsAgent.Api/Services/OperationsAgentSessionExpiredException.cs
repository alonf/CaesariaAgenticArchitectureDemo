namespace OperationsAgent.Api.Services;

/// <summary>
/// Thrown when a follow-up question references a conversational session that is unknown or expired,
/// so the caller can report it explicitly instead of silently starting an empty conversation.
/// </summary>
public sealed class OperationsAgentSessionExpiredException(string sessionId)
    : InvalidOperationException($"Conversational session {sessionId} is unknown or has expired.")
{
    /// <summary>
    /// Gets the session identifier that could not be resumed.
    /// </summary>
    public string SessionId { get; } = sessionId;
}
