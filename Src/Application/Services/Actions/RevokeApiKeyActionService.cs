namespace Application.Services.Actions;

using System;
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
/// Revokes a single action from an API key.
/// </summary>
public sealed class RevokeApiKeyActionService : IRevokeApiKeyActionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdempotencyKeyRepository _idempotencyKeyRepository;
    private readonly ICryptoService _cryptoService;

    /// <summary>
    /// Initializes a new instance of the <see cref="RevokeApiKeyActionService"/> class.
    /// </summary>
    /// <param name="unitOfWork">Unit of work for repository access.</param>
    /// <param name="idempotencyKeyRepository">Repository for looking up idempotency keys.</param>
    /// <param name="cryptoService">Service used to hash the idempotency key for lookup.</param>
    public RevokeApiKeyActionService(
        IUnitOfWork unitOfWork,
        IIdempotencyKeyRepository idempotencyKeyRepository,
        ICryptoService cryptoService)
    {
        _unitOfWork = unitOfWork;
        _idempotencyKeyRepository = idempotencyKeyRepository;
        _cryptoService = cryptoService;
    }

    /// <summary>
    /// Revokes an action from an API key by soft-deleting the active grant.
    /// </summary>
    /// <param name="idempotencyKey">The idempotency key that identifies the API key.</param>
    /// <param name="actionName">The name of the action to revoke.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="NotFoundException">
    /// Thrown when no API key exists for the given <paramref name="idempotencyKey"/> or the action is not currently granted.
    /// </exception>
    /// <exception cref="ValidationException">Thrown when the action name is not a defined action.</exception>
    public async Task ExecuteAsync(
        string idempotencyKey,
        string actionName,
        CancellationToken cancellationToken = default)
    {
        ApiKeyActionEnum action = ApiKeyActionParser.Parse(actionName);

        string idempotencyKeyHash = _cryptoService.HashForLookup(idempotencyKey);

        IdempotencyKey idempotencyKeyEntity = await _idempotencyKeyRepository.GetByHashAsync(idempotencyKeyHash, cancellationToken)
            ?? throw new NotFoundException("No API key matches the provided idempotency key.");

        Guid apiKeyId = idempotencyKeyEntity.ApiKeyId;

        bool removed = await _unitOfWork.ApiKeyActions.RemoveAsync(apiKeyId, action, cancellationToken);

        if (!removed)
        {
            throw new NotFoundException($"The Action: {action} is not granted to the ApiKey");
        }
    }
}
