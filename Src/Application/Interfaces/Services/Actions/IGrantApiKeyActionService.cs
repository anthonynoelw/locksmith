namespace Application.Interfaces.Services.Actions;
using Domain.Enums;
using Domain.Models;

/// <summary>
/// Grants a single action to an API key.
/// </summary>
public interface IGrantApiKeyActionService
{
    /// <summary>
    /// Grants an action to an API key.
    /// </summary>
    /// <param name="apiKeyId">The ID of the API key.</param>
    /// <param name="action">The action to grant.</param>
    /// <param name="createdBy">The identity of the caller granting the action.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The newly granted action.</returns>
    public Task<ApiKeyAction> ExecuteAsync(
        Guid apiKeyId,
        ApiKeyActionEnum action,
        string createdBy,
        CancellationToken cancellationToken = default);
}
