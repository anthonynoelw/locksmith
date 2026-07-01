namespace Application.Interfaces.Repositories;

using Domain.Models;

/// <summary>
/// Provides data access operations for API Key status history.
/// </summary>
/// <remarks>
/// This repository enforces append-only semantics — status records are never updated or deleted,
/// only new records are appended to create an immutable audit trail.
/// </remarks>
public interface IApiKeyStatusRepository
{
    /// <summary>
    /// Gets all status records for an API Key, ordered by creation date ascending.
    /// </summary>
    /// <param name="apiKeyId">The API Key identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of status records for the API Key; empty if none exist.</returns>
    public Task<IReadOnlyList<ApiKeyStatus>> GetByApiKeyIdAsync(
        Guid apiKeyId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the current (most recent) status for an API Key.
    /// </summary>
    /// <param name="apiKeyId">The API Key identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The most recent status record; null if no status history exists.</returns>
    public Task<ApiKeyStatus?> GetCurrentStatusAsync(
        Guid apiKeyId,
        CancellationToken ct = default);

    /// <summary>
    /// Appends a new status record for an API Key (append-only operation).
    /// </summary>
    /// <param name="status">The status record to append.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task AddAsync(ApiKeyStatus status, CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes the current (most recent, non-deleted) status for an API Key.
    /// </summary>
    /// <param name="apiKeyId">The API Key identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="Domain.Exceptions.NotFoundException">Thrown when no current status exists for the given <paramref name="apiKeyId"/>.</exception>
    /// <exception cref="Domain.Exceptions.ConflictException">Thrown when the current status is <see cref="Domain.Enums.ApiKeyStatusEnum.Revoked"/> or <see cref="Domain.Enums.ApiKeyStatusEnum.Expired"/> and therefore cannot be changed.</exception>
    public Task SoftDeleteAsync(
        Guid apiKeyId,
        CancellationToken ct = default);
}
