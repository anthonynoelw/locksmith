namespace Infrastructure.Repositories;

using Application.Interfaces.Repositories;
using Domain.Models;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Provides EF Core-based data access operations for API Keys.
/// </summary>
public sealed class ApiKeyRepository(AppDbContext db) : IApiKeyRepository
{
    /// <summary>
    /// Gets an API Key by its unique identifier.
    /// </summary>
    /// <param name="id">The API Key identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The API Key if found; otherwise null.</returns>
    public async Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.ApiKeys
            .Include(k => k.Statuses)
            .Include(k => k.Actions)
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Id == id, ct);

    /// <summary>
    /// Gets an API Key by its idempotency key hash.
    /// </summary>
    /// <param name="idempotencyKeyHash">The idempotency key hash.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The API Key if found; otherwise null.</returns>
    public async Task<ApiKey?> GetByIdempotencyKeyHashAsync(
        string idempotencyKeyHash,
        CancellationToken ct = default) =>
        await db.ApiKeys
            .Include(k => k.Statuses)
            .Include(k => k.Actions)
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.IdempotencyKeyHash == idempotencyKeyHash, ct);

    /// <summary>
    /// Gets all API Keys.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of all API Keys; empty if none exist.</returns>
    public async Task<IReadOnlyList<ApiKey>> GetAllAsync(CancellationToken ct = default) =>
        await db.ApiKeys
            .Include(k => k.Statuses)
            .Include(k => k.Actions)
            .AsNoTracking()
            .ToListAsync(ct);

    /// <summary>
    /// Adds a new API Key to the repository.
    /// </summary>
    /// <param name="apiKey">The API Key to add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AddAsync(ApiKey apiKey, CancellationToken ct = default)
    {
        db.ApiKeys.Add(apiKey);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Soft-deletes an existing API Key by setting its DeletedAt timestamp.
    /// </summary>
    /// <param name="id">The API Key identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        ApiKey? apiKey = await db.ApiKeys.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (apiKey is not null)
        {
            apiKey.DeletedAt = DateTime.UtcNow;
            db.ApiKeys.Update(apiKey);
            await db.SaveChangesAsync(ct);
        }
    }
}
