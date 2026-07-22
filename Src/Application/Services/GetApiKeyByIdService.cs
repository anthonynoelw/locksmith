namespace Application.Services;

using Application.Interfaces;
using Application.Interfaces.Services;
using Domain.Exceptions;
using Domain.Models;

/// <summary>Retrieves an API key's metadata by its identifier.</summary>
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
    public async Task<ApiKeyMetadata> ExecuteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ApiKey? apiKey = await _unitOfWork.ApiKeys.GetByIdAsync(id, cancellationToken);

        // A key always gets an initial status row at creation, so having none left that are not
        // soft-deleted only happens once the key itself has been deleted.
        if (apiKey is null || apiKey.Statuses.All(s => s.DeletedAt is not null))
        {
            throw new NotFoundException($"API key with ID {id} not found.");
        }

        return ApiKeyMetadataMapper.ToMetadata(apiKey);
    }
}
