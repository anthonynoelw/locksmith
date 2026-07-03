namespace Application.Services.Status;

using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Interfaces.Services.Status;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Models;

/// <summary>
/// Updates the status of an API key.
/// </summary>
public sealed class UpdateApiKeyStatusService : IUpdateApiKeyStatusService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICryptoService _cryptoService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateApiKeyStatusService"/> class.
    /// </summary>
    /// <param name="unitOfWork">Unit of work for repository access.</param>
    /// <param name="cryptoService">The service used to hash the idempotency key for lookup.</param>
    public UpdateApiKeyStatusService(IUnitOfWork unitOfWork, ICryptoService cryptoService)
    {
        _unitOfWork = unitOfWork;
        _cryptoService = cryptoService;
    }

    /// <summary>
    /// Updates the status of an API key identified by its idempotency key.
    /// </summary>
    /// <param name="idempotencyKey">The idempotency key of the API key.</param>
    /// <param name="status">The new status to apply.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="NotFoundException">Thrown when <paramref name="idempotencyKey"/> is invalid or no current status exists for the resolved API key.</exception>
    /// <exception cref="ConflictException">Thrown when the API key's current status is <see cref="ApiKeyStatusEnum.Revoked"/> or <see cref="ApiKeyStatusEnum.Expired"/> and therefore cannot be changed.</exception>
    /// <remarks>This method soft-deletes the current status and appends the new one.</remarks>
    public async Task ExecuteAsync(
        string idempotencyKey,
        ApiKeyStatusEnum status,
        CancellationToken cancellationToken = default)
    {
        string idempotencyKeyHash = _cryptoService.HashForLookup(idempotencyKey);
        IdempotencyKey idempotencyKeyEntity = await _unitOfWork.IdempotencyKeys.GetByHashAsync(
            idempotencyKeyHash,
            cancellationToken) ?? throw new NotFoundException("Invalid idempotency key.");

        await _unitOfWork.ApiKeyStatuses.SoftDeleteAsync(idempotencyKeyEntity.ApiKeyId, cancellationToken);

        await _unitOfWork.ApiKeyStatuses.AddAsync(
            new ApiKeyStatus()
            {
                ApiKeyId = idempotencyKeyEntity.ApiKeyId,
                ApiKey = idempotencyKeyEntity.ApiKey,
                Status = status,
            }, cancellationToken);
    }
}
