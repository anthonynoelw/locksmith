namespace Application.Services;

using Application.Commands;
using Application.Interfaces;
using Application.Interfaces.Services;
using Domain;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Models;

/// <summary>Rotates an API key by deleting the current key and issuing a brand new one with the same actions.</summary>
public sealed class RotateApiKeyService : IRotateApiKeyService
{
    private readonly ICreateApiKeyService _createApiKeyService;
    private readonly IDeleteApiKeyService _deleteApiKeyService;
    private readonly ICryptoService _cryptoService;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>Initializes a new instance of the <see cref="RotateApiKeyService"/> class.</summary>
    /// <param name="createApiKeyService">Service that creates the replacement API key.</param>
    /// <param name="deleteApiKeyService">Service that deletes the current API key.</param>
    /// <param name="unitOfWork">Unit of work for repository access.</param>
    /// <param name="cryptoService">Service used to hash the idempotency key for lookup.</param>
    public RotateApiKeyService(
        ICreateApiKeyService createApiKeyService,
        IDeleteApiKeyService deleteApiKeyService,
        IUnitOfWork unitOfWork,
        ICryptoService cryptoService)
    {
        _createApiKeyService = createApiKeyService;
        _deleteApiKeyService = deleteApiKeyService;
        _unitOfWork = unitOfWork;
        _cryptoService = cryptoService;
    }

    /// <summary>
    /// Rotates the API key identified by <paramref name="idempotencyKey"/>: deletes it and issues a new
    /// key carrying the same granted actions, atomically.
    /// </summary>
    /// <param name="idempotencyKey">The plaintext idempotency key that identifies the API key.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The newly created API key ID, plaintext secret, and idempotency key.</returns>
    /// <exception cref="NotFoundException">Thrown when no API key exists for the given <paramref name="idempotencyKey"/>.</exception>
    public async Task<CreateApiKeyResult> ExecuteAsync(string idempotencyKey, CancellationToken ct)
    {
        CreateApiKeyResult? result = null;

        string idempotencyKeyHash = _cryptoService.HashForLookup(idempotencyKey);

        IdempotencyKey idempotencyKeyEntity = await _unitOfWork.IdempotencyKeys.GetByHashAsync(idempotencyKeyHash, ct)
            ?? throw new NotFoundException($"API key with idempotency key {idempotencyKeyHash} not found.");

        IReadOnlyList<ApiKeyActionEnum> actions = (await _unitOfWork.ApiKeyActions.GetActiveByApiKeyIdAsync(
            idempotencyKeyEntity.ApiKeyId,
            ct))
            .Select(a => a.Action)
            .ToList();

        await _unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                await _deleteApiKeyService.ExecuteAsync(idempotencyKey, ct);

                result = await _createApiKeyService.ExecuteAsync(
                    new CreateApiKeyCommand(WellKnown.CallerIdentities.API_CLIENT, null, actions),
                    ct);
            },
            ct);

        return result ?? throw new ConflictException("A new API Key could not be created");
    }
}
