namespace Application.Interfaces.Repositories;

using Domain.Enums;
using Domain.Models;

/// <summary>
/// Provides data access operations for API Key action permissions.
/// </summary>
public interface IApiKeyActionRepository
{
    /// <summary>
    /// Gets all actions ever granted to an API Key, including soft-deleted (revoked) rows.
    /// </summary>
    /// <param name="apiKeyId">The API Key identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of granted actions; empty if none exist.</returns>
    public Task<IReadOnlyList<ApiKeyAction>> GetByApiKeyIdAsync(
        Guid apiKeyId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the currently granted (non-revoked) actions of an API Key.
    /// </summary>
    /// <param name="apiKeyId">The API Key identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of active actions; empty if none exist.</returns>
    public Task<IReadOnlyList<ApiKeyAction>> GetActiveByApiKeyIdAsync(
        Guid apiKeyId,
        CancellationToken ct = default);

    /// <summary>
    /// Adds a single action to an API Key.
    /// </summary>
    /// <param name="action">The action to add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="Domain.Exceptions.ConflictException">
    /// Thrown when the action is already actively granted to the API Key (concurrent grant).
    /// </exception>
    public Task AddAsync(ApiKeyAction action, CancellationToken ct = default);

    /// <summary>
    /// Removes a single action from an API Key.
    /// </summary>
    /// <param name="apiKeyId">The API Key identifier.</param>
    /// <param name="actionType">The action to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if removed; false if the action was not granted.</returns>
    public Task<bool> RemoveAsync(
        Guid apiKeyId,
        ApiKeyActionEnum actionType,
        CancellationToken ct = default);

    /// <summary>
    /// Removes all actions from an API Key.
    /// </summary>
    /// <param name="apiKeyId">The API Key identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task RemoveAllAsync(Guid apiKeyId, CancellationToken ct = default);
}
