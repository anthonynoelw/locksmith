namespace Domain.Exceptions;

/// <summary>Thrown when cryptographic decryption fails due to authentication or corruption.</summary>
public sealed class DecryptionFailedException : DomainException
{
    /// <summary>Initializes a new instance of the <see cref="DecryptionFailedException"/> class.</summary>
    public DecryptionFailedException()
        : base()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DecryptionFailedException"/> class.</summary>
    /// <param name="message">The exception message.</param>
    public DecryptionFailedException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DecryptionFailedException"/> class.</summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public DecryptionFailedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
