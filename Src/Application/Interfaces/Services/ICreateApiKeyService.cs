namespace Application.Interfaces.Services;

using Application.Commands;

/// <summary>
/// Creates a new API key, generating its secret and idempotency key.
/// </summary>
/// <remarks>
/// This service orchestrates the complete API key creation use case, generating
/// cryptographically secure secrets and idempotency keys, persisting the key to storage,
/// and returning the plaintext credentials to the caller.
/// </remarks>
public interface ICreateApiKeyService
{
    /// <summary>Executes the create API key use case.</summary>
    /// <param name="command">The creation parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created API key ID, plaintext secret, and idempotency key.</returns>
    public Task<CreateApiKeyResult> Execute(CreateApiKeyCommand command, CancellationToken cancellationToken = default);
}
