namespace Domain.Exceptions;

/// <summary>
/// Thrown when attempting to update or delete an append-only entity. Maps to HTTP 409.
/// </summary>
public sealed class AppendOnlyViolationException : ConflictException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppendOnlyViolationException"/> class.
    /// </summary>
    /// <param name="entityType">The type of the entity being modified.</param>
    /// <param name="operation">The operation attempted (Update or Delete).</param>
    public AppendOnlyViolationException(string entityType, string operation)
        : base($"{entityType} is append-only and cannot be {operation.ToLowerInvariant()}ed")
    {
        ArgumentException.ThrowIfNullOrEmpty(entityType, nameof(entityType));
        ArgumentException.ThrowIfNullOrEmpty(operation, nameof(operation));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AppendOnlyViolationException"/> class.
    /// </summary>
    /// <param name="message">Message Parameter.</param>
    public AppendOnlyViolationException(string message)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrEmpty(message, nameof(message));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AppendOnlyViolationException"/> class.
    /// </summary>
    /// <param name="message">Message Parameter.</param>
    /// <param name="innerException">Inner Exception Parameter.</param>
    public AppendOnlyViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrEmpty(message, nameof(message));
        ArgumentNullException.ThrowIfNull(innerException, nameof(innerException));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AppendOnlyViolationException"/> class.
    /// </summary>
    public AppendOnlyViolationException()
        : base()
    {
    }
}
