namespace OperationsAgent.Api.Services;

/// <summary>
/// Indicates that the investigation model returned a response that could not be parsed or did not satisfy the
/// required evidence-discipline schema. This exception surfaces as a clear failure; the Operations Agent API
/// never substitutes a fabricated or success-shaped result when the model response is invalid.
/// </summary>
public sealed class InvestigationResponseFormatException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvestigationResponseFormatException"/> class.
    /// </summary>
    public InvestigationResponseFormatException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvestigationResponseFormatException"/> class with a message.
    /// </summary>
    /// <param name="message">The message describing why the response was rejected.</param>
    public InvestigationResponseFormatException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvestigationResponseFormatException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The message describing why the response was rejected.</param>
    /// <param name="innerException">The underlying exception, such as a JSON parsing failure.</param>
    public InvestigationResponseFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
