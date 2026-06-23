namespace Application.Interfaces.Services;

using Domain.Models;

/// <summary>
/// Retrieves an API key by its identifier.
/// </summary>
public interface IGetApiKeyByIdService
{
    /// <summary>Gets an API key by its ID.</summary>
    /// <param name="id">The API key identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The API key.</returns>
    /// <exception cref="NotFoundException">Thrown when no API key with the specified ID exists.</exception>
    public Task<ApiKey> Execute(Guid id, CancellationToken cancellationToken = default);
}
