namespace Infrastructure.Repositories;

using Application.Interfaces.Repositories;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Models;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Provides EF Core-based data access operations for API Key status history.
/// </summary>
/// <remarks>
/// This repository enforces append-only semantics — status records are never updated or deleted,
/// only new records are appended to create an immutable audit trail.
/// </remarks>
public sealed class ApiKeyStatusRepository(AppDbContext db) : IApiKeyStatusRepository
{
    /// <summary>
    /// Gets all status records for an API Key, ordered by creation date ascending.
    /// </summary>
    /// <param name="apiKeyId">The API Key identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of status records for the API Key; empty if none exist.</returns>
    public async Task<IReadOnlyList<ApiKeyStatus>> GetByApiKeyIdAsync(
        Guid apiKeyId,
        CancellationToken ct = default) =>
        await db.ApiKeyStatuses
            .Where(s => s.ApiKeyId == apiKeyId)
            .OrderBy(s => s.CreatedAt)
            .AsNoTracking()
            .ToListAsync(ct);

    /// <summary>
    /// Gets the current (most recent) status for an API Key.
    /// </summary>
    /// <param name="apiKeyId">The API Key identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The most recent status record; null if no status history exists.</returns>
    public async Task<ApiKeyStatus?> GetCurrentStatusAsync(
        Guid apiKeyId,
        CancellationToken ct = default) =>
        await db.ApiKeyStatuses
            .Where(s => s.ApiKeyId == apiKeyId)
            .OrderByDescending(s => s.CreatedAt)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Appends a new status record for an API Key (append-only operation).
    /// </summary>
    /// <param name="status">The status record to append.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AddAsync(ApiKeyStatus status, CancellationToken ct = default)
    {
        // The status's ApiKey navigation may come from a no-tracking query (e.g. resolved via
        // an idempotency key lookup). Without this, EF Core treats the detached ApiKey as a new
        // entity to insert alongside the status, causing a primary key violation on save.
        if (db.Entry(status.ApiKey).State == EntityState.Detached)
        {
            db.Attach(status.ApiKey);
        }

        db.ApiKeyStatuses.Add(status);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Soft-deletes the current (most recent, non-deleted) status for an API Key.
    /// </summary>
    /// <param name="apiKeyId">The API Key identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="NotFoundException">Thrown when no current status exists for the given <paramref name="apiKeyId"/>.</exception>
    /// <exception cref="ConflictException">Thrown when the current status is <see cref="ApiKeyStatusEnum.Revoked"/> or <see cref="ApiKeyStatusEnum.Expired"/> and therefore cannot be changed.</exception>
    public async Task SoftDeleteAsync(
        Guid apiKeyId,
        CancellationToken ct = default)
    {
        ApiKeyStatus status = await db.ApiKeyStatuses
            .Where(s => s.ApiKeyId == apiKeyId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(s => s.DeletedAt == null, ct) ?? throw new NotFoundException($"Api Key Status with ID {apiKeyId} was not found");

        if (status.Status is ApiKeyStatusEnum.Revoked or ApiKeyStatusEnum.Expired)
        {
            throw new ConflictException($"The current Status: {status.Status} of the ApiKey cannot be changed");
        }

        status.DeletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
    }
}
