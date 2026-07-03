namespace Application.Services;

using Application.Interfaces;
using Application.Interfaces.Services;
using Domain.Exceptions;
using Domain.Models;

/// <summary>Retrieves an API key by its identifier.</summary>
public sealed class GetApiKeyByIdService : IGetApiKeyByIdService
{
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>Initializes a new instance of the <see cref="GetApiKeyByIdService"/> class.</summary>
    /// <param name="unitOfWork">Unit of work for repository access.</param>
    public GetApiKeyByIdService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc/>
    public async Task<ApiKey> ExecuteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ApiKey? apiKey = await _unitOfWork.ApiKeys.GetByIdAsync(id, cancellationToken);

        if (apiKey is null)
        {
            throw new NotFoundException($"API key with ID {id} not found.");
        }

        return apiKey;
    }
}
