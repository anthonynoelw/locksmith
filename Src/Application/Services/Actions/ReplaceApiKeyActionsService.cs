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
/// Replaces the full action set of an API key.
/// </summary>
public sealed class ReplaceApiKeyActionsService : IReplaceApiKeyActionsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdempotencyKeyRepository _idempotencyKeyRepository;
    private readonly ICryptoService _cryptoService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReplaceApiKeyActionsService"/> class.
    /// </summary>
    /// <param name="unitOfWork">Unit of work for repository access.</param>
    /// <param name="idempotencyKeyRepository">Repository for looking up idempotency keys.</param>
    /// <param name="cryptoService">Service used to hash the idempotency key for lookup.</param>
    public ReplaceApiKeyActionsService(
        IUnitOfWork unitOfWork,
        IIdempotencyKeyRepository idempotencyKeyRepository,
        ICryptoService cryptoService)
    {
        _unitOfWork = unitOfWork;
        _idempotencyKeyRepository = idempotencyKeyRepository;
        _cryptoService = cryptoService;
    }

    /// <summary>
    /// Replaces the currently granted actions of an API key with the requested set,
    /// revoking removed actions and granting added ones atomically.
    /// </summary>
    /// <param name="idempotencyKey">The idempotency key that identifies the API key.</param>
    /// <param name="actions">The desired set of granted actions.</param>
    /// <param name="createdBy">The identity of the caller replacing the actions.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resulting active actions granted to the API key.</returns>
    /// <exception cref="NotFoundException">
    /// Thrown when no API key exists for the given <paramref name="idempotencyKey"/>.
    /// </exception>
    /// <exception cref="ValidationException">Thrown when the set contains an undefined action value.</exception>
    public async Task<IReadOnlyList<ApiKeyAction>> ExecuteAsync(
        string idempotencyKey,
        IReadOnlyList<ApiKeyActionEnum> actions,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        ApiKeyActionParser.ValidateDefined(actions);

        string idempotencyKeyHash = _cryptoService.HashForLookup(idempotencyKey);

        IdempotencyKey idempotencyKeyEntity = await _idempotencyKeyRepository.GetByHashAsync(idempotencyKeyHash, cancellationToken)
            ?? throw new NotFoundException("No API key matches the provided idempotency key.");

        Guid apiKeyId = idempotencyKeyEntity.ApiKeyId;
        ApiKey apiKey = idempotencyKeyEntity.ApiKey;

        IReadOnlyList<ApiKeyAction> active = await _unitOfWork.ApiKeyActions.GetActiveByApiKeyIdAsync(
            apiKeyId,
            cancellationToken);

        var activeActions = active.Select(a => a.Action).ToHashSet();
        var requested = actions.ToHashSet();
        var granted = new List<ApiKeyAction>();

        // A single transaction keeps the replacement atomic: a mid-way failure must not leave the
        // key with a partially applied set (e.g. the union of the old and new permissions).
        await _unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                foreach (ApiKeyActionEnum action in activeActions.Except(requested))
                {
                    await _unitOfWork.ApiKeyActions.RemoveAsync(apiKeyId, action, cancellationToken);
                }

                foreach (ApiKeyActionEnum action in requested.Except(activeActions))
                {
                    var apiKeyAction = new ApiKeyAction
                    {
                        Id = Guid.NewGuid(),
                        ApiKeyId = apiKeyId,
                        Action = action,
                        CreatedBy = createdBy,
                        CreatedAt = DateTime.UtcNow,
                        DeletedAt = null,
                        ApiKey = apiKey,
                    };

                    await _unitOfWork.ApiKeyActions.AddAsync(apiKeyAction, cancellationToken);
                    granted.Add(apiKeyAction);
                }
            },
            cancellationToken);

        return active
            .Where(a => requested.Contains(a.Action))
            .Concat(granted)
            .ToList();
    }
}
