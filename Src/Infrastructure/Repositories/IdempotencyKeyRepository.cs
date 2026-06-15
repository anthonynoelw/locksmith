namespace Infrastructure.Repositories;

using Application.Interfaces.Repositories;
using Domain.Models;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Provides data access operations for <see cref="IdempotencyKey"/> entities.
/// </summary>
public sealed class IdempotencyKeyRepository(AppDbContext db) : IIdempotencyKeyRepository
{
    /// <inheritdoc/>
    public async Task<IdempotencyKey?> GetByHashAsync(string idempotencyKeyHash, CancellationToken ct = default) =>
        await db.IdempotencyKeys
            .Include(e => e.ApiKey)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.IdempotencyKeyHash == idempotencyKeyHash, ct);

    /// <inheritdoc/>
    public async Task AddAsync(IdempotencyKey idempotencyKey, CancellationToken ct = default)
    {
        db.IdempotencyKeys.Add(idempotencyKey);
        await db.SaveChangesAsync(ct);
    }
}
