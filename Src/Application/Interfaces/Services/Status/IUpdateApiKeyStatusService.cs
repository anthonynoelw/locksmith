namespace Application.Interfaces.Services.Status;

using Domain.Enums;

/// <summary>
/// Updates the status of an API key.
/// </summary>
public interface IUpdateApiKeyStatusService
{
    /// <summary>
    /// Updates the status of an API key identified by its idempotency key.
    /// </summary>
    /// <param name="idempotencyKey">The idempotency key of the API key.</param>
    /// <param name="status">The new status to apply.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task ExecuteAsync(string idempotencyKey, ApiKeyStatusEnum status, CancellationToken cancellationToken = default);
}
