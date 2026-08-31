namespace OperationsAgent.Api.Services;

/// <summary>
/// Indicates that an Operations Agent request exceeded its configured execution budget.
/// </summary>
public sealed class OperationsAgentTimedOutException(string message, Exception innerException)
    : TimeoutException(message, innerException);
