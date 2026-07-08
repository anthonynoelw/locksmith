namespace Application.Interfaces.Services;

/// <summary>
/// Deletes an API key identified by its idempotency key.
/// </summary>
public interface IDeleteApiKeyService
{
    /// <summary>
    /// Soft-deletes the active statuses and actions of the API key identified by <paramref name="idempotencyKey"/>.
    /// </summary>
    /// <param name="idempotencyKey">The plaintext idempotency key that identifies the API key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c> once the matching API key has been deleted.</returns>
    public Task<bool> ExecuteAsync(string idempotencyKey, CancellationToken cancellationToken);
}
