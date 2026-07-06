namespace Application.Services.Actions;

using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces;
using Application.Interfaces.Services.Actions;
using Domain.Enums;
using Domain.Exceptions;

/// <summary>
/// Revokes a single action from an API key.
/// </summary>
public sealed class RevokeApiKeyActionService : IRevokeApiKeyActionService
{
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="RevokeApiKeyActionService"/> class.
    /// </summary>
    /// <param name="unitOfWork">Unit of work for repository access.</param>
    public RevokeApiKeyActionService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Revokes an action from an API key by soft-deleting the active grant.
    /// </summary>
    /// <param name="apiKeyId">The ID of the API key.</param>
    /// <param name="action">The action to revoke.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="NotFoundException">
    /// Thrown when no API key exists for the given <paramref name="apiKeyId"/> or the action is not currently granted.
    /// </exception>
    public async Task ExecuteAsync(
        Guid apiKeyId,
        ApiKeyActionEnum action,
        CancellationToken cancellationToken = default)
    {
        _ = await _unitOfWork.ApiKeys.GetByIdAsync(apiKeyId, cancellationToken)
            ?? throw new NotFoundException($"API key with ID {apiKeyId} not found.");

        bool removed = await _unitOfWork.ApiKeyActions.RemoveAsync(apiKeyId, action, cancellationToken);

        if (!removed)
        {
            throw new NotFoundException($"The Action: {action} is not granted to the ApiKey");
        }
    }
}
