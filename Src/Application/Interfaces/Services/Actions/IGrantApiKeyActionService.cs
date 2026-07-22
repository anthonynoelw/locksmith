namespace Application.Interfaces.Services.Actions;

using Domain.Models;

/// <summary>
/// Grants a single action to an API key.
/// </summary>
public interface IGrantApiKeyActionService
{
    /// <summary>
    /// Grants an action to an API key.
    /// </summary>
    /// <param name="idempotencyKey">The idempotency key that identifies the API key.</param>
    /// <param name="actionName">The name of the action to grant.</param>
    /// <param name="createdBy">The identity of the caller granting the action.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The newly granted action.</returns>
    /// <exception cref="Domain.Exceptions.ValidationException">Thrown when the action name is not a defined action.</exception>
    public Task<ApiKeyAction> ExecuteAsync(
        string idempotencyKey,
        string actionName,
        string createdBy,
        CancellationToken cancellationToken = default);
}
