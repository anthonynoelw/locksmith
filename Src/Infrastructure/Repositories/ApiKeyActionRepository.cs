namespace Infrastructure.Repositories;

using Application.Interfaces.Repositories;
using Domain.Enums;
using Domain.Models;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Provides EF Core-based data access operations for API Key action permissions.
/// </summary>
public sealed class ApiKeyActionRepository(AppDbContext db) : IApiKeyActionRepository
{
    /// <summary>
    /// Gets all allowed actions for an API Key.
    /// </summary>
    /// <param name="apiKeyId">The API Key identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of granted actions; empty if none exist.</returns>
    public async Task<IReadOnlyList<ApiKeyAction>> GetByApiKeyIdAsync(
        Guid apiKeyId,
        CancellationToken ct = default) =>
        await db.ApiKeyActions
            .Where(a => a.ApiKeyId == apiKeyId)
            .AsNoTracking()
            .ToListAsync(ct);

    /// <summary>
    /// Adds a single action to an API Key.
    /// </summary>
    /// <param name="action">The action to add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AddAsync(ApiKeyAction action, CancellationToken ct = default)
    {
        db.ApiKeyActions.Add(action);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Soft-deletes a single action from an API Key by setting its DeletedAt timestamp.
    /// </summary>
    /// <param name="apiKeyId">The API Key identifier.</param>
    /// <param name="actionType">The action to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if removed; false if the action was not granted.</returns>
    public async Task<bool> RemoveAsync(
        Guid apiKeyId,
        ApiKeyActionEnum actionType,
        CancellationToken ct = default)
    {
        ApiKeyAction? action = await db.ApiKeyActions
            .FirstOrDefaultAsync(a => a.ApiKeyId == apiKeyId && a.Action == actionType, ct);

        if (action is null)
        {
            return false;
        }

        action.DeletedAt = DateTime.UtcNow;
        db.ApiKeyActions.Update(action);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Soft-deletes all actions from an API Key by setting their DeletedAt timestamps.
    /// </summary>
    /// <param name="apiKeyId">The API Key identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RemoveAllAsync(Guid apiKeyId, CancellationToken ct = default)
    {
        List<ApiKeyAction> actions = await db.ApiKeyActions
            .Where(a => a.ApiKeyId == apiKeyId && a.DeletedAt == null)
            .ToListAsync(ct);

        DateTime now = DateTime.UtcNow;
        foreach (ApiKeyAction action in actions)
        {
            action.DeletedAt = now;
        }

        db.ApiKeyActions.UpdateRange(actions);
        await db.SaveChangesAsync(ct);
    }
}
