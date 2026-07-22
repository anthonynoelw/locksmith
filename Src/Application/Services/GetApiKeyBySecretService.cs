namespace Application.Services;

using Application.Interfaces;
using Application.Interfaces.Services;
using Domain.Exceptions;
using Domain.Models;

/// <summary>Resolves the identifier of an API key from its plaintext secret.</summary>
public sealed class GetApiKeyBySecretService : IGetApiKeyBySecretService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICryptoService _cryptoService;

    /// <summary>Initializes a new instance of the <see cref="GetApiKeyBySecretService"/> class.</summary>
    /// <param name="unitOfWork">Unit of work for repository access.</param>
    /// <param name="cryptoService">Cryptographic operations provider.</param>
    public GetApiKeyBySecretService(IUnitOfWork unitOfWork, ICryptoService cryptoService)
    {
        _unitOfWork = unitOfWork;
        _cryptoService = cryptoService;
    }

    /// <inheritdoc/>
    public async Task<Guid> ExecuteAsync(string secret, CancellationToken cancellationToken = default)
    {
        string secretHash = _cryptoService.HashForLookup(secret);

        ApiKey apiKey = await _unitOfWork.ApiKeys.GetBySecretHashAsync(secretHash, cancellationToken)
            ?? throw new NotFoundException("API key with the provided secret not found.");

        return apiKey.Id;
    }
}
