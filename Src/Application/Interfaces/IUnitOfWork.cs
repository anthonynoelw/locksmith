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
    /// Saves all pending changes to the database within the current transaction.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of state entries written to the database.</returns>
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
