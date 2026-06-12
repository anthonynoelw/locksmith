namespace Domain.Models;

using Domain.Enums;

/// <summary>
/// Represents the action of an API key.
/// </summary>
public class ApiKeyAction : IAppendOnlyTable
{
    /// <summary>
    /// Gets the unique identifier for the API key action.
    /// </summary>
    public required Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the API key for the API key action.
    /// </summary>
    public required Guid ApiKeyId { get; init; }

    /// <summary>
    /// Gets the action of the API key.
    /// </summary>
    public required ApiKeyActionEnum Action { get; init; }

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
