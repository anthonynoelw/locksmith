namespace Application.Services;

using Application.Interfaces;
using Application.Interfaces.Services;
using Domain.Models;

/// <summary>Lists API keys with pagination support.</summary>
public sealed class ListApiKeysService : IListApiKeysService
{
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>Initializes a new instance of the <see cref="ListApiKeysService"/> class.</summary>
    /// <param name="unitOfWork">Unit of work for repository access.</param>
    public ListApiKeysService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc/>
    public async Task<ListApiKeysResult> ExecuteAsync(
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0 || limit > 1000)
        {
            limit = 50;
        }

        if (offset < 0)
        {
            offset = 0;
        }

        (IReadOnlyList<ApiKey> keys, int totalCount) = await _unitOfWork.ApiKeys.GetPageAsync(offset, limit, cancellationToken);

        return new ListApiKeysResult(keys.ToList(), totalCount, limit, offset);
    }
}
