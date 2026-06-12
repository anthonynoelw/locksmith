namespace Application.Interfaces.Repositories;

using Domain.Enums;
using Domain.Models;

/// <summary>
/// Provides data access operations for API Key action permissions.
/// </summary>
public interface IApiKeyActionRepository
{
    /// <summary>
    /// Gets all allowed actions for an API Key.
    /// </summary>
    /// <param name="apiKeyId">The API Key identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of granted actions; empty if none exist.</returns>
    public Task<IReadOnlyList<ApiKeyAction>> GetByApiKeyIdAsync(
        Guid apiKeyId,
        CancellationToken ct = default);

    /// <summary>
    /// Adds a single action to an API Key.
    /// </summary>
    /// <param name="action">The action to add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
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
