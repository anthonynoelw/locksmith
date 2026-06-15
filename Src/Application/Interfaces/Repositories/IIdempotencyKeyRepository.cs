namespace Application.Interfaces.Repositories;

using Domain.Models;

/// <summary>
/// Provides data access operations for <see cref="IdempotencyKey"/> entities.
/// </summary>
public interface IIdempotencyKeyRepository
{
    /// <summary>
    /// Retrieves an idempotency key by its hash.
    /// </summary>
    /// <param name="idempotencyKeyHash">The SHA-256 hash of the plaintext idempotency key.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The <see cref="IdempotencyKey"/> if found; otherwise <c>null</c>.</returns>
    public Task<IdempotencyKey?> GetByHashAsync(string idempotencyKeyHash, CancellationToken ct = default);

    /// <summary>
    /// Adds a new idempotency key to the repository.
    /// </summary>
    /// <param name="idempotencyKey">The idempotency key to add.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task AddAsync(IdempotencyKey idempotencyKey, CancellationToken ct = default);
}
