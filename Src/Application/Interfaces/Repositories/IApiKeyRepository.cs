namespace Application.Interfaces.Repositories;

using Domain.Models;

/// <summary>
/// Provides data access operations for API Keys.
/// </summary>
public interface IApiKeyRepository
{
    /// <summary>
    /// Gets an API Key by its unique identifier.
    /// </summary>
    /// <param name="id">The API Key identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The API Key if found; otherwise null.</returns>
    public Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets an API Key by its secret hash.
    /// </summary>
    /// <param name="secretHash">The SHA-256 hash of the plaintext API key secret.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The API Key if found; otherwise null.</returns>
    public Task<ApiKey?> GetBySecretHashAsync(string secretHash, CancellationToken ct = default);

    /// <summary>
    /// Gets all API Keys.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of all API Keys; empty if none exist.</returns>
    public Task<IReadOnlyList<ApiKey>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Adds a new API Key to the repository.
    /// </summary>
    /// <param name="apiKey">The API Key to add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task AddAsync(ApiKey apiKey, CancellationToken ct = default);

    /// <summary>
    /// Deletes an existing API Key.
    /// </summary>
    /// <param name="id">The API Key with updated values.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task DeleteAsync(Guid id, CancellationToken ct = default);
}
