namespace Application.Interfaces.Services;

/// <summary>
/// Retrieves and decrypts an API key secret using its idempotency key.
/// </summary>
public interface IRetrieveSecretService
{
    /// <summary>Retrieves and decrypts an API key secret using its idempotency key.</summary>
    /// <param name="idempotencyKey">The plaintext idempotency key for decryption.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The API key ID and decrypted plaintext secret.</returns>
    public Task<RetrieveSecretResult> ExecuteAsync(string idempotencyKey, CancellationToken cancellationToken = default);
}

/// <summary>Result of retrieving and decrypting a secret.</summary>
/// <param name="ApiKeyId">The API key identifier.</param>
/// <param name="Secret">The decrypted plaintext secret.</param>
public sealed record RetrieveSecretResult(Guid ApiKeyId, string Secret);
