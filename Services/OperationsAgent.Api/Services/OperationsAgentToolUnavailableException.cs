namespace OperationsAgent.Api.Services;

/// <summary>
/// Thrown when the agent's remote tooling cannot be reached or does not offer a required tool -
/// MCP transport or protocol failures, and missing tools at discovery time. Mapped to a
/// presenter-friendly 502 by the ask endpoint.
/// </summary>
public sealed class OperationsAgentToolUnavailableException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OperationsAgentToolUnavailableException"/> class.
    /// </summary>
    /// <param name="message">The presenter-friendly failure description.</param>
    /// <param name="innerException">The underlying failure, when one exists.</param>
    public OperationsAgentToolUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
