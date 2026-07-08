namespace Application.Interfaces.Services.Actions;

using Domain.Enums;

/// <summary>
/// Revokes a single action from an API key.
/// </summary>
public interface IRevokeApiKeyActionService
{
    /// <summary>
    /// Revokes an action from an API key by soft-deleting the active grant.
    /// </summary>
    /// <param name="idempotencyKeyHash">The hash of the idempotency key that identifies the API key.</param>
    /// <param name="action">The action to revoke.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task ExecuteAsync(
        string idempotencyKeyHash,
        ApiKeyActionEnum action,
        CancellationToken cancellationToken = default);
}
