namespace OperationsAgent.Api.Services;

/// <summary>
/// Represents an investigation that could not obtain the minimum authoritative evidence required for a result.
/// </summary>
public sealed class InvestigationEvidenceUnavailableException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvestigationEvidenceUnavailableException"/> class.
    /// </summary>
    /// <param name="message">The error message describing the missing authoritative evidence.</param>
    public InvestigationEvidenceUnavailableException(string message)
        : base(message)
    {
    }
}
