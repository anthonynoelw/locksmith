namespace Application.Services.Actions;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
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
    private readonly ICryptoService _cryptoService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GrantApiKeyActionService"/> class.
    /// </summary>
    /// <param name="unitOfWork">Unit of work for repository access.</param>
    /// <param name="idempotencyKeyRepository">Repository for looking up idempotency keys.</param>
    /// <param name="cryptoService">Service used to hash the idempotency key for lookup.</param>
    public GrantApiKeyActionService(
        IUnitOfWork unitOfWork,
        IIdempotencyKeyRepository idempotencyKeyRepository,
        ICryptoService cryptoService)
    {
        _unitOfWork = unitOfWork;
        _idempotencyKeyRepository = idempotencyKeyRepository;
        _cryptoService = cryptoService;
    }

    /// <summary>
    /// Grants an action to an API key by appending a new action row.
    /// </summary>
    /// <param name="idempotencyKey">The idempotency key that identifies the API key.</param>
    /// <param name="actionName">The name of the action to grant.</param>
    /// <param name="createdBy">The identity of the caller granting the action.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The newly granted action.</returns>
    /// <exception cref="NotFoundException">Thrown when no API key exists for the given <paramref name="idempotencyKey"/>.</exception>
    /// <exception cref="ConflictException">Thrown when the action is already actively granted to the API key.</exception>
    /// <exception cref="ValidationException">Thrown when the action name is not a defined action.</exception>
    public async Task<ApiKeyAction> ExecuteAsync(
        string idempotencyKey,
        string actionName,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        ApiKeyActionEnum action = ApiKeyActionParser.Parse(actionName);

        string idempotencyKeyHash = _cryptoService.HashForLookup(idempotencyKey);

        IdempotencyKey idempotencyKeyEntity = await _idempotencyKeyRepository.GetByHashAsync(idempotencyKeyHash, cancellationToken)
            ?? throw new NotFoundException("No API key matches the provided idempotency key.");

        Guid apiKeyId = idempotencyKeyEntity.ApiKeyId;

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
            ApiKey = idempotencyKeyEntity.ApiKey,
        };

        await _unitOfWork.ApiKeyActions.AddAsync(apiKeyAction, cancellationToken);

        return apiKeyAction;
    }
}
