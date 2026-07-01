namespace Application.Services;

using Application.Interfaces;
using Application.Interfaces.Services;
using Domain.Exceptions;

/// <summary>Retrieves and decrypts an API key secret using its idempotency key.</summary>
public sealed class RetrieveSecretService : IRetrieveSecretService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICryptoService _cryptoService;

    /// <summary>Initializes a new instance of the <see cref="RetrieveSecretService"/> class.</summary>
    /// <param name="unitOfWork">Unit of work for repository access.</param>
    /// <param name="cryptoService">Cryptographic operations provider.</param>
    public RetrieveSecretService(IUnitOfWork unitOfWork, ICryptoService cryptoService)
    {
        _unitOfWork = unitOfWork;
        _cryptoService = cryptoService;
    }

    /// <inheritdoc/>
    public async Task<RetrieveSecretResult> ExecuteAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        string idempotencyKeyHash = _cryptoService.HashForLookup(idempotencyKey);
        Domain.Models.IdempotencyKey? idempotencyKeyEntity = await _unitOfWork.IdempotencyKeys.GetByHashAsync(
            idempotencyKeyHash,
            cancellationToken);

        if (idempotencyKeyEntity is null)
        {
            throw new NotFoundException("Invalid idempotency key.");
        }

        byte[] encryptionKey = _cryptoService.DeriveEncryptionKey(
            idempotencyKey,
            Convert.FromBase64String(idempotencyKeyEntity.Salt));

        string plainSecret;
        try
        {
            plainSecret = _cryptoService.Decrypt(idempotencyKeyEntity.ApiKey.Secret, encryptionKey);
        }
        catch (Exception ex) when (!(ex is DomainException))
        {
            throw new DecryptionFailedException("Failed to decrypt API key secret.", ex);
        }

        return new RetrieveSecretResult(idempotencyKeyEntity.ApiKeyId, plainSecret);
    }
}
