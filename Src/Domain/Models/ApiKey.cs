namespace Domain.Models;

/// <summary>
/// Represents an API key and its associated metadata.
/// </summary>
public class ApiKey : IAppendOnlyTable
{
    /// <summary>
    /// Gets the unique identifier for the API key.
    /// </summary>
    public required Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the encrypted ciphertext of the API key secret.
    /// Decrypted using a key derived from <see cref="IdempotencyKey"/> and <see cref="Salt"/>.
    /// </summary>
    public required string Secret { get; init; }

    /// <summary>
    /// Gets the API key hash.
    /// </summary>
    public required string SecretHash { get; init; }

    /// <summary>
    /// Gets the date and time the API key was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the identity of the caller who created the API key.
    /// </summary>
    public required string CreatedBy { get; init; }

    /// <summary>
    /// Gets the date and time the API key expires.
    /// </summary>
    public required DateTime ExpiresAt { get; init; } = DateTime.UtcNow.AddDays(30);

    /// <summary>
    /// Gets or sets the statuses of the API key.
    /// </summary>
    /// <remarks>
    /// The statuses are stored in the database as a collection of <see cref="ApiKeyStatus"/> objects.
    /// </remarks>
    public required ICollection<ApiKeyStatus> Statuses { get; set; } = new List<ApiKeyStatus>();

    /// <summary>
    /// Gets or sets the actions of the API key.
    /// </summary>
    /// <remarks>
    /// The actions are stored in the database as a collection of <see cref="ApiKeyAction"/> objects.
    /// </remarks>
    public required ICollection<ApiKeyAction> Actions { get; set; } = new List<ApiKeyAction>();
}
