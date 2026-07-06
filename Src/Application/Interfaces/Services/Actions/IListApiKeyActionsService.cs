namespace Application.Interfaces.Services.Actions;
using Domain.Models;

/// <summary>
/// Lists the actions granted to an API key.
/// </summary>
public interface IListApiKeyActionsService
{
    /// <summary>
    /// Gets all currently granted (non-revoked) actions of an API key.
    /// </summary>
    /// <param name="apiKeyId">The ID of the API key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The active actions granted to the API key.</returns>
    public Task<IReadOnlyList<ApiKeyAction>> ExecuteAsync(
        Guid apiKeyId,
        CancellationToken cancellationToken = default);
}
