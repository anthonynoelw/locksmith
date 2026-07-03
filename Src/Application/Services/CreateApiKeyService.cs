namespace Application.Services;

using System.Security.Cryptography;
using Application.Commands;
using Application.Interfaces;
using Application.Interfaces.Services;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Models;

/// <summary>Creates a new API key, generating its idempotency key and encrypted secret.</summary>
public sealed class CreateApiKeyService : ICreateApiKeyService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICryptoService _cryptoService;

    /// <summary>Initializes a new instance of the <see cref="CreateApiKeyService"/> class.</summary>
    /// <param name="unitOfWork">Unit of work for repository access.</param>
    /// <param name="cryptoService">Cryptographic operations provider.</param>
    public CreateApiKeyService(IUnitOfWork unitOfWork, ICryptoService cryptoService)
    {
        _unitOfWork = unitOfWork;
        _cryptoService = cryptoService;
    }

    /// <inheritdoc/>
    public async Task<CreateApiKeyResult> ExecuteAsync(CreateApiKeyCommand command, CancellationToken cancellationToken = default)
    {
        if (command.ExpiresAt <= DateTime.UtcNow)
        {
            throw new ValidationException(
                "Validation failed.",
                new Dictionary<string, string[]>
                {
                    { "ExpiresAt", new[] { "ExpiresAt must be in the future." } },
                });
        }

        string idempotencyKey = _cryptoService.GenerateIdempotencyKey();
        string idempotencyKeyHash = _cryptoService.HashForLookup(idempotencyKey);

        byte[] salt = RandomNumberGenerator.GetBytes(32);
        byte[] encKey = _cryptoService.DeriveEncryptionKey(idempotencyKey, salt);

        string plainSecret = _cryptoService.GenerateApiKeySecret();
        string secretHash = _cryptoService.HashForLookup(plainSecret);
        string ciphertext = _cryptoService.Encrypt(plainSecret, encKey);

        DateTime now = DateTime.UtcNow;

        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            Secret = ciphertext,
            SecretHash = secretHash,
            CreatedBy = command.CreatedBy,
            CreatedAt = now,
            ExpiresAt = command.ExpiresAt ?? now.AddDays(30),
            Statuses = new List<ApiKeyStatus>(),
            Actions = new List<ApiKeyAction>(),
        };

        await _unitOfWork.ApiKeys.AddAsync(apiKey, cancellationToken);

        var idempotencyKey_entity = new IdempotencyKey
        {
            Id = Guid.NewGuid(),
            ApiKeyId = apiKey.Id,
            IdempotencyKeyHash = idempotencyKeyHash,
            Salt = Convert.ToBase64String(salt),
            CreatedBy = command.CreatedBy,
            CreatedAt = now,
            ApiKey = apiKey,
        };

        await _unitOfWork.IdempotencyKeys.AddAsync(idempotencyKey_entity, cancellationToken);

        await _unitOfWork.ApiKeyStatuses.AddAsync(
            new ApiKeyStatus
            {
                Id = Guid.NewGuid(),
                ApiKeyId = apiKey.Id,
                Status = ApiKeyStatusEnum.Inactive,
                CreatedBy = command.CreatedBy,
                CreatedAt = now,
                DeletedAt = null,
                ApiKey = apiKey,
            },
            cancellationToken);

        foreach (ApiKeyActionEnum action in command.Actions)
        {
            await _unitOfWork.ApiKeyActions.AddAsync(
                new ApiKeyAction
                {
                    Id = Guid.NewGuid(),
                    ApiKeyId = apiKey.Id,
                    Action = action,
                    CreatedBy = command.CreatedBy,
                    CreatedAt = now,
                    DeletedAt = null,
                    ApiKey = apiKey,
                },
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateApiKeyResult(apiKey.Id, plainSecret, idempotencyKey);
    }
}
