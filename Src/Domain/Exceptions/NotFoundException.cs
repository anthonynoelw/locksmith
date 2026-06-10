namespace Domain.Exceptions;

/// <summary>Thrown when a requested resource cannot be found. Maps to HTTP 404.</summary>
public sealed class NotFoundException : DomainException
{
    /// <summary>Initializes a new instance of the <see cref="NotFoundException"/> class.</summary>
    /// <param name="message">A description identifying the missing resource.</param>
    public NotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NotFoundException"/> class.</summary>
    /// <param name="message">A description identifying the missing resource.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public NotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotFoundException"/> class.
    /// </summary>
    public NotFoundException()
        : base()
    {
    }
}
