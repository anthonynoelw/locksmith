namespace Application.Interfaces.Services;

using Application.Services;

/// <summary>
/// Retrieves an API key's metadata by its identifier.
/// </summary>
public interface IGetApiKeyByIdService
{
    /// <summary>Gets an API key's metadata by its ID.</summary>
    /// <param name="id">The API key identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The API key metadata.</returns>
    /// <exception cref="Domain.Exceptions.NotFoundException">Thrown when no API key with the specified ID exists.</exception>
    public Task<ApiKeyMetadata> ExecuteAsync(Guid id, CancellationToken cancellationToken = default);
}
