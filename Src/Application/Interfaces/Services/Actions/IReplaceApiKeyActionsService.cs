namespace Application.Interfaces.Services.Actions;

using Domain.Enums;
using Domain.Models;

/// <summary>
/// Replaces the full action set of an API key.
/// </summary>
public interface IReplaceApiKeyActionsService
{
    /// <summary>
    /// Replaces the currently granted actions of an API key with the requested set,
    /// revoking removed actions and granting added ones.
    /// </summary>
    /// <param name="idempotencyKey">The idempotency key that identifies the API key.</param>
    /// <param name="actions">The desired set of granted actions.</param>
    /// <param name="createdBy">The identity of the caller replacing the actions.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resulting active actions granted to the API key.</returns>
    /// <exception cref="Domain.Exceptions.ValidationException">Thrown when the set contains an undefined action value.</exception>
    public Task<IReadOnlyList<ApiKeyAction>> ExecuteAsync(
        string idempotencyKey,
        IReadOnlyList<ApiKeyActionEnum> actions,
        string createdBy,
        CancellationToken cancellationToken = default);
}
