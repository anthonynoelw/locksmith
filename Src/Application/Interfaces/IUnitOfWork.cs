namespace Application.Interfaces;

using Application.Interfaces.Repositories;

/// <summary>
/// Provides transaction and repository coordination for data access operations.
/// </summary>
/// <remarks>
/// The Unit of Work pattern ensures that multiple repository operations are coordinated
/// within a single database transaction, maintaining consistency across related data changes.
/// </remarks>
public interface IUnitOfWork : IAsyncDisposable
{
    /// <summary>
    /// Gets the repository for API Key operations.
    /// </summary>
    public IApiKeyRepository ApiKeys { get; }

    /// <summary>
    /// Gets the repository for API Key status history operations.
    /// </summary>
    public IApiKeyStatusRepository ApiKeyStatuses { get; }

    /// <summary>
    /// Gets the repository for API Key action permission operations.
    /// </summary>
    public IApiKeyActionRepository ApiKeyActions { get; }

    /// <summary>
    /// Gets the repository for idempotency key operations.
    /// </summary>
    public IIdempotencyKeyRepository IdempotencyKeys { get; }

    /// <summary>
    /// Saves all pending changes to the database within the current transaction.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of state entries written to the database.</returns>
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="operation"/> inside a single database transaction, committing on
    /// success and rolling back every repository write if it throws.
    /// </summary>
    /// <param name="operation">The operation performing one or more repository writes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default);
}
