namespace Application.Interfaces.Services;

using Domain.Models;

/// <summary>
/// Lists API keys with pagination support.
/// </summary>
public interface IListApiKeysService
{
    /// <summary>Lists all API keys with pagination.</summary>
    /// <param name="limit">Maximum number of keys to return (1-1000, default 50).</param>
    /// <param name="offset">Number of keys to skip (default 0).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated list of API keys.</returns>
    public Task<ListApiKeysResult> Execute(int limit, int offset, CancellationToken cancellationToken = default);
}

/// <summary>Result of listing API keys.</summary>
/// <param name="Keys">The paginated list of API keys.</param>
/// <param name="Total">Total count of all API keys.</param>
/// <param name="Limit">The limit applied to the query.</param>
/// <param name="Offset">The offset applied to the query.</param>
public sealed record ListApiKeysResult(List<ApiKey> Keys, int Total, int Limit, int Offset);
