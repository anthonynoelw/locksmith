namespace Application.Interfaces.Services;

using Application.Commands;

/// <summary>
/// Rotates an API key by deleting the current key and issuing a brand new one with the same actions.
/// </summary>
public interface IRotateApiKeyService
{
    /// <summary>
    /// Rotates the API key identified by <paramref name="idempotencyKey"/>: deletes it and issues a new
    /// key carrying the same granted actions, atomically.
    /// </summary>
    /// <param name="idempotencyKey">The plaintext idempotency key that identifies the API key.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The newly created API key ID, plaintext secret, and idempotency key.</returns>
    public Task<CreateApiKeyResult> ExecuteAsync(string idempotencyKey, CancellationToken ct);
}
