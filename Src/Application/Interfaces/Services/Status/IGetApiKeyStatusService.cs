namespace Application.Interfaces.Services.Status;
using Domain.Models;

/// <summary>
/// Gets the current status of an API key.
/// </summary>
public interface IGetApiKeyStatusService
{
    /// <summary>
    /// Gets the current status of an API key.
    /// </summary>
    /// <param name="id">The ID of the API key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current status of the API key.</returns>
    /// <exception cref="Domain.Exceptions.NotFoundException">Thrown when no current status exists for the given <paramref name="id"/>.</exception>
    public Task<ApiKeyStatus> ExecuteAsync(Guid id, CancellationToken cancellationToken = default);
}
