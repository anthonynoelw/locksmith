namespace Application.Infrastructure;

using global::Application.Interfaces.Services;

/// <summary>
/// A deterministic <see cref="IGetApiKeyBySecretService"/> for tests that always resolves a fixed
/// secret to a fixed API key ID, letting HTTP-level tests exercise <c>X-Api-Key</c>-resolved endpoints
/// without a live database.
/// </summary>
public sealed class FakeGetApiKeyBySecretService : IGetApiKeyBySecretService
{
    private readonly string _secret;
    private readonly Guid _apiKeyId;

    /// <summary>Initializes a new instance of the <see cref="FakeGetApiKeyBySecretService"/> class.</summary>
    /// <param name="secret">The plaintext secret that resolves successfully.</param>
    /// <param name="apiKeyId">The API key ID returned for <paramref name="secret"/>.</param>
    public FakeGetApiKeyBySecretService(string secret, Guid apiKeyId)
    {
        _secret = secret;
        _apiKeyId = apiKeyId;
    }

    /// <inheritdoc/>
    public Task<Guid> ExecuteAsync(string secret, CancellationToken cancellationToken = default)
    {
        return secret == _secret
            ? Task.FromResult(_apiKeyId)
            : throw new Domain.Exceptions.NotFoundException("No API key matches the presented secret.");
    }
}
