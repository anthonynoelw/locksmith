namespace Application.Interfaces.Services.Status;
using Domain.Models;

/// <summary>
/// Gets the status history of an API key.
/// </summary>
public interface IGetApiKeyStatusHistoryService
{
    /// <summary>
    /// Gets all status changes of an API key.
    /// </summary>
    /// <param name="id">The ID of the API key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>All recorded statuses for the API key.</returns>
    public Task<IReadOnlyList<ApiKeyStatus>> ExecuteAsync(Guid id, CancellationToken cancellationToken = default);
}
