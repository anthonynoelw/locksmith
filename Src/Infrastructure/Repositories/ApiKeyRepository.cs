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
    /// Gets an API Key by its secret hash.
    /// </summary>
    /// <param name="secretHash">The SHA-256 hash of the plaintext API key secret.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The API Key if found; otherwise null.</returns>
    public async Task<ApiKey?> GetBySecretHashAsync(
        string secretHash,
        CancellationToken ct = default) =>
        await db.ApiKeys
            .Include(k => k.Statuses)
            .Include(k => k.Actions)
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.SecretHash == secretHash, ct);

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
    /// Soft-deletes an existing API Key by marking its related statuses and actions as deleted.
    /// </summary>
    /// <param name="id">The API Key identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        List<ApiKeyStatus> statuses = await db.ApiKeyStatuses
            .Where(s => s.ApiKeyId == id && s.DeletedAt == null)
            .ToListAsync(ct);

        List<ApiKeyAction> actions = await db.ApiKeyActions
            .Where(a => a.ApiKeyId == id && a.DeletedAt == null)
            .ToListAsync(ct);

        if (statuses.Count > 0 || actions.Count > 0)
        {
            foreach (ApiKeyStatus status in statuses)
            {
                status.DeletedAt = DateTime.UtcNow;
            }

            foreach (ApiKeyAction action in actions)
            {
                action.DeletedAt = DateTime.UtcNow;
            }

            db.ApiKeyStatuses.UpdateRange(statuses);
            db.ApiKeyActions.UpdateRange(actions);
            await db.SaveChangesAsync(ct);
        }
    }
}
