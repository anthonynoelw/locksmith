namespace Application.Interfaces.Services;

/// <summary>
/// Resolves the identifier of an API key from its plaintext secret.
/// </summary>
public interface IGetApiKeyBySecretService
{
    /// <summary>Resolves the API key identifier for the given plaintext secret.</summary>
    /// <param name="secret">The plaintext API key secret.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The identifier of the API key that owns the secret.</returns>
    /// <exception cref="Domain.Exceptions.NotFoundException">Thrown when no API key matches the secret.</exception>
    public Task<Guid> ExecuteAsync(string secret, CancellationToken cancellationToken = default);
}
