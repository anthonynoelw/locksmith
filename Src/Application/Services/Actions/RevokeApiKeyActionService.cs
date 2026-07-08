namespace Application.Services.Actions;

using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services.Actions;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Models;

/// <summary>
/// Revokes a single action from an API key.
/// </summary>
public sealed class RevokeApiKeyActionService : IRevokeApiKeyActionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdempotencyKeyRepository _idempotencyKeyRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="RevokeApiKeyActionService"/> class.
    /// </summary>
    /// <param name="unitOfWork">Unit of work for repository access.</param>
    /// <param name="idempotencyKeyRepository">Repository for looking up idempotency keys.</param>
    public RevokeApiKeyActionService(IUnitOfWork unitOfWork, IIdempotencyKeyRepository idempotencyKeyRepository)
    {
        _unitOfWork = unitOfWork;
        _idempotencyKeyRepository = idempotencyKeyRepository;
    }

    /// <summary>
    /// Revokes an action from an API key by soft-deleting the active grant.
    /// </summary>
    /// <param name="idempotencyKeyHash">The hash of the idempotency key that identifies the API key.</param>
    /// <param name="action">The action to revoke.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="NotFoundException">
    /// Thrown when no API key exists for the given <paramref name="idempotencyKeyHash"/> or the action is not currently granted.
    /// </exception>
    public async Task ExecuteAsync(
        string idempotencyKeyHash,
        ApiKeyActionEnum action,
        CancellationToken cancellationToken = default)
    {
        IdempotencyKey idempotencyKey = await _idempotencyKeyRepository.GetByHashAsync(idempotencyKeyHash, cancellationToken)
            ?? throw new NotFoundException($"API key with idempotency key {idempotencyKeyHash} not found.");

        Guid apiKeyId = idempotencyKey.ApiKeyId;

        bool removed = await _unitOfWork.ApiKeyActions.RemoveAsync(apiKeyId, action, cancellationToken);

        if (!removed)
        {
            throw new NotFoundException($"The Action: {action} is not granted to the ApiKey");
        }
    }
}
