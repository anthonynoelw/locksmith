namespace Infrastructure;

using Application.Interfaces;
using Application.Interfaces.Repositories;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore.Storage;

/// <summary>
/// Provides transaction and repository coordination for data access operations.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="UnitOfWork"/> class.
/// </remarks>
public sealed class UnitOfWork(AppDbContext dbContext) : IUnitOfWork
{
    private IApiKeyRepository? _apiKeyRepository;
    private IApiKeyStatusRepository? _apiKeyStatusRepository;
    private IApiKeyActionRepository? _apiKeyActionRepository;
    private IIdempotencyKeyRepository? _idempotencyKeyRepository;
    private bool _disposed;

    /// <summary>
    /// Gets the repository for API Key operations.
    /// </summary>
    public IApiKeyRepository ApiKeys => _apiKeyRepository ??= new ApiKeyRepository(dbContext);

    /// <summary>
    /// Gets the repository for API Key status history operations.
    /// </summary>
    public IApiKeyStatusRepository ApiKeyStatuses => _apiKeyStatusRepository ??= new ApiKeyStatusRepository(dbContext);

    /// <summary>
    /// Gets the repository for API Key action permission operations.
    /// </summary>
    public IApiKeyActionRepository ApiKeyActions => _apiKeyActionRepository ??= new ApiKeyActionRepository(dbContext);

    /// <summary>
    /// Gets the repository for idempotency key operations.
    /// </summary>
    public IIdempotencyKeyRepository IdempotencyKeys => _idempotencyKeyRepository ??= new IdempotencyKeyRepository(dbContext);

    /// <summary>
    /// Saves all pending changes to the database within the current transaction.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of state entries written to the database.</returns>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Runs <paramref name="operation"/> inside a single database transaction, committing on
    /// success and rolling back every repository write if it throws.
    /// </summary>
    /// <param name="operation">The operation performing one or more repository writes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ExecuteInTransactionAsync(
        Func<Task> operation,
        CancellationToken cancellationToken = default)
    {
        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await operation();
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Disposes the unit of work and its associated database context.
    /// </summary>
    /// <returns>A Value Task.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await dbContext.DisposeAsync();
        _disposed = true;
    }
}
