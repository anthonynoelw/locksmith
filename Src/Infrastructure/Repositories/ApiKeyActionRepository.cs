namespace Infrastructure.Repositories;

using Application.Interfaces.Repositories;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Models;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

/// <summary>
/// Provides EF Core-based data access operations for API Key action permissions.
/// </summary>
public sealed class ApiKeyActionRepository(AppDbContext db) : IApiKeyActionRepository
{
    /// <summary>
    /// Gets all actions ever granted to an API Key, including soft-deleted (revoked) rows.
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
    /// Gets the currently granted (non-revoked) actions of an API Key.
    /// </summary>
    /// <param name="apiKeyId">The API Key identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of active actions; empty if none exist.</returns>
    public async Task<IReadOnlyList<ApiKeyAction>> GetActiveByApiKeyIdAsync(
        Guid apiKeyId,
        CancellationToken ct = default) =>
        await db.ApiKeyActions
            .Where(a => a.ApiKeyId == apiKeyId && a.DeletedAt == null)
            .AsNoTracking()
            .ToListAsync(ct);

    /// <summary>
    /// Adds a single action to an API Key.
    /// </summary>
    /// <param name="action">The action to add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ConflictException">
    /// Thrown when the action is already actively granted to the API Key (concurrent grant).
    /// </exception>
    public async Task AddAsync(ApiKeyAction action, CancellationToken ct = default)
    {
        // Setting the entry state directly attaches only the action itself. Add() would cascade to
        // the detached ApiKey navigation graph (loaded via no-tracking queries), attempting to
        // re-insert existing rows and coupling callers to a fragile attach ordering.
        db.Entry(action).State = EntityState.Added;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
            && pg.ConstraintName == AppDbContext.ACTIVE_ACTION_UNIQUE_INDEX)
        {
            // A concurrent request granted the same action between the caller's duplicate check and
            // this insert; the partial unique index is the authoritative guard.
            throw new ConflictException($"The Action: {action.Action} is already granted to the ApiKey");
        }
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
            .FirstOrDefaultAsync(a => a.ApiKeyId == apiKeyId && a.Action == actionType && a.DeletedAt == null, ct);

        if (action is null)
        {
            return false;
        }

        // The entity is tracked, so setting DeletedAt is enough. Update() would mark every property
        // as modified, which the append-only guard in AppDbContext.SaveChanges rejects.
        action.DeletedAt = DateTime.UtcNow;
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

        // The entities are tracked, so setting DeletedAt is enough. UpdateRange() would mark every
        // property as modified, which the append-only guard in AppDbContext.SaveChanges rejects.
        DateTime now = DateTime.UtcNow;
        foreach (ApiKeyAction action in actions)
        {
            action.DeletedAt = now;
        }

        await db.SaveChangesAsync(ct);
    }
}
