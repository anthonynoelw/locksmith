namespace Application.Services.Actions;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services.Actions;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Models;

/// <summary>
/// Grants a single action to an API key.
/// </summary>
public sealed class GrantApiKeyActionService : IGrantApiKeyActionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdempotencyKeyRepository _idempotencyKeyRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GrantApiKeyActionService"/> class.
    /// </summary>
    /// <param name="unitOfWork">Unit of work for repository access.</param>
    /// <param name="idempotencyKeyRepository">Repository for looking up idempotency keys.</param>
    public GrantApiKeyActionService(IUnitOfWork unitOfWork, IIdempotencyKeyRepository idempotencyKeyRepository)
    {
        _unitOfWork = unitOfWork;
        _idempotencyKeyRepository = idempotencyKeyRepository;
    }

    /// <summary>
    /// Grants an action to an API key by appending a new action row.
    /// </summary>
    /// <param name="idempotencyKeyHash">The hash of the idempotency key that identifies the API key.</param>
    /// <param name="action">The action to grant.</param>
    /// <param name="createdBy">The identity of the caller granting the action.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The newly granted action.</returns>
    /// <exception cref="NotFoundException">Thrown when no API key exists for the given <paramref name="idempotencyKeyHash"/>.</exception>
    /// <exception cref="ConflictException">Thrown when the action is already actively granted to the API key.</exception>
    public async Task<ApiKeyAction> ExecuteAsync(
        string idempotencyKeyHash,
        ApiKeyActionEnum action,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        IdempotencyKey idempotencyKey = await _idempotencyKeyRepository.GetByHashAsync(idempotencyKeyHash, cancellationToken)
            ?? throw new NotFoundException($"API key with idempotency key {idempotencyKeyHash} not found.");

        Guid apiKeyId = idempotencyKey.ApiKeyId;

        IReadOnlyList<ApiKeyAction> activeActions = await _unitOfWork.ApiKeyActions.GetActiveByApiKeyIdAsync(
            apiKeyId,
            cancellationToken);

        if (activeActions.Any(a => a.Action == action))
        {
            throw new ConflictException($"The Action: {action} is already granted to the ApiKey");
        }

        var apiKeyAction = new ApiKeyAction
        {
            Id = Guid.NewGuid(),
            ApiKeyId = apiKeyId,
            Action = action,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            DeletedAt = null,
            ApiKey = idempotencyKey.ApiKey,
        };

        await _unitOfWork.ApiKeyActions.AddAsync(apiKeyAction, cancellationToken);

        return apiKeyAction;
    }
}
