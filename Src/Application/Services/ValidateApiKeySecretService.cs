namespace Application.Services;

using Application.Interfaces;
using Application.Interfaces.Services;
using Domain.Enums;
using Domain.Exceptions;

/// <summary>Validates an API key secret and returns its current status.</summary>
public sealed class ValidateApiKeySecretService : IValidateApiKeySecretService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICryptoService _cryptoService;

    /// <summary>Initializes a new instance of the <see cref="ValidateApiKeySecretService"/> class.</summary>
    /// <param name="unitOfWork">Unit of work for repository access.</param>
    /// <param name="cryptoService">Cryptographic operations provider.</param>
    public ValidateApiKeySecretService(IUnitOfWork unitOfWork, ICryptoService cryptoService)
    {
        _unitOfWork = unitOfWork;
        _cryptoService = cryptoService;
    }

    /// <inheritdoc/>
    public async Task<ValidateApiKeySecretResult> ExecuteAsync(
        string secret,
        CancellationToken cancellationToken = default)
    {
        string secretHash = _cryptoService.HashForLookup(secret);
        Domain.Models.ApiKey? apiKey = await _unitOfWork.ApiKeys.GetBySecretHashAsync(
            secretHash,
            cancellationToken);

        if (apiKey is null)
        {
            throw new NotFoundException("API key with the provided secret not found.");
        }

        Domain.Models.ApiKeyStatus? currentStatus = apiKey.Statuses
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefault();

        string statusString = currentStatus?.Status.ToString() ?? ApiKeyStatusEnum.Inactive.ToString();
        bool isValid = currentStatus?.Status == ApiKeyStatusEnum.Active;

        return new ValidateApiKeySecretResult(apiKey.Id, isValid, statusString);
    }
}
