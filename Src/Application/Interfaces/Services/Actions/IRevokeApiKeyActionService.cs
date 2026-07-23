namespace Application.Interfaces.Services.Actions;

/// <summary>
/// Revokes a single action from an API key.
/// </summary>
public interface IRevokeApiKeyActionService
{
    /// <summary>
    /// Revokes an action from an API key by soft-deleting the active grant.
    /// </summary>
    /// <param name="idempotencyKey">The idempotency key that identifies the API key.</param>
    /// <param name="actionName">The name of the action to revoke.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="Domain.Exceptions.ValidationException">Thrown when the action name is not a defined action.</exception>
    public Task ExecuteAsync(
        string idempotencyKey,
        string actionName,
        CancellationToken cancellationToken = default);
}
