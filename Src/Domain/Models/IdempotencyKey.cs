namespace Domain.Models;

/// <summary>
/// Represents the idempotency key for an API key.
/// </summary>
public class IdempotencyKey : IAppendOnlyTable
{
    /// <summary>
    /// Gets the unique identifier for the idempotency key.
    /// </summary>
    public required Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the unique identifier for the API key.
    /// </summary>
    public required Guid ApiKeyId { get; init; }

    /// <summary>
    /// Gets the idempotency key hash for the API key.
    /// </summary>
    public required string IdempotencyKeyHash { get; init; }

    /// <summary>
    /// Gets the salt used to derive the decryption key for <see cref="Secret"/>.
    /// </summary>
    public required string Salt { get; init; }

    /// <summary>
    /// Gets the date and time the API key action was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the identity of the caller who created the API key action.
    /// </summary>
    public required string CreatedBy { get; init; }

    /// <summary>
    /// Gets or sets the date and time the API key action was soft-deleted.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Gets the API key for the API key action.
    /// </summary>
    /// <remarks>
    /// The API key is stored in the database as a <see cref="ApiKey"/> object.
    /// </remarks>
    public required ApiKey ApiKey { get; init; }
}
