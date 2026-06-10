namespace Domain.Exceptions;

/// <summary>Thrown when an operation would violate a uniqueness or state constraint. Maps to HTTP 409.</summary>
public class ConflictException : DomainException
{
    /// <summary>Initializes a new instance of the <see cref="ConflictException"/> class.</summary>
    /// <param name="message">A description of the conflicting state.</param>
    public ConflictException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ConflictException"/> class.</summary>
    /// <param name="message">A description of the conflicting state.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public ConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConflictException"/> class.
    /// </summary>
    public ConflictException()
        : base()
    {
    }
}
