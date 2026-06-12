namespace Domain.Models;

using Domain.Enums;

/// <summary>
/// Represents the status of an API key.
/// </summary>
public class ApiKeyStatus : IAppendOnlyTable
{
    /// <summary>
    /// Gets the unique identifier for the API key status.
    /// </summary>
    public required Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the API key for the API key status.
    /// </summary>
    public required Guid ApiKeyId { get; init; }

    /// <summary>
    /// Gets the status of the API key.
    /// </summary>
    public required ApiKeyStatusEnum Status { get; init; }

    /// <summary>
    /// Gets the date and time the API key status was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the identity of the caller who created the API key status.
    /// </summary>
    public required string CreatedBy { get; init; }

    /// <summary>
    /// Gets or sets the date and time the API key status was soft-deleted.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Gets the API key for the API key status.
    /// </summary>
    /// <remarks>
    /// The API key is stored in the database as a <see cref="ApiKey"/> object.
    /// </remarks>
    public required ApiKey ApiKey { get; init; }
}
