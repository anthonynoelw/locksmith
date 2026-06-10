namespace Domain.Exceptions;

/// <summary>Base class for all domain-layer exceptions. Represents a business rule violation.</summary>
public abstract class DomainException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="DomainException"/> class.</summary>
    /// <param name="message">A human-readable description of the violation.</param>
    protected DomainException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DomainException"/> class.</summary>
    /// <param name="message">A human-readable description of the violation.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    protected DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainException"/> class.
    /// </summary>
    protected DomainException()
        : base()
    {
    }
}
