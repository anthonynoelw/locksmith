namespace Application.Services;

using Application.Interfaces;
using Application.Interfaces.Services;
using Domain.Models;
using Domain.Exceptions;

/// <summary>Deletes an API key identified by its idempotency key.</summary>
public sealed class DeleteApiKeyService : IDeleteApiKeyService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICryptoService _cryptoService;

    /// <summary>Initializes a new instance of the <see cref="DeleteApiKeyService"/> class.</summary>
    /// <param name="unitOfWork">Unit of work for repository access.</param>
    /// <param name="cryptoService">Service used to hash the idempotency key for lookup.</param>
    public DeleteApiKeyService(IUnitOfWork unitOfWork, ICryptoService cryptoService)
    {
        _unitOfWork = unitOfWork;
        _cryptoService = cryptoService;
    }

    /// <summary>
    /// Soft-deletes the active statuses and actions of the API key identified by <paramref name="idempotencyKey"/>.
    /// </summary>
    /// <param name="idempotencyKey">The plaintext idempotency key that identifies the API key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c> once the matching API key has been deleted.</returns>
    /// <exception cref="NotFoundException">Thrown when no API key exists for the given <paramref name="idempotencyKey"/>.</exception>
    public async Task<bool> ExecuteAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        string idempotencyKeyHash = _cryptoService.HashForLookup(idempotencyKey);

        IdempotencyKey idempotencyKeyEntity = await _unitOfWork.IdempotencyKeys.GetByHashAsync(
            idempotencyKeyHash,
            cancellationToken)
            ?? throw new NotFoundException("No API key matches the provided idempotency key.");

        await _unitOfWork.ApiKeys.DeleteAsync(idempotencyKeyEntity.ApiKeyId, cancellationToken);

        return true;
    }
}
