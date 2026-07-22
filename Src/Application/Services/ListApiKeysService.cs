namespace Application.Services;

using System.Linq;
using Application.Interfaces;
using Application.Interfaces.Services;
using Domain.Models;

/// <summary>Lists API keys with pagination support.</summary>
public sealed class ListApiKeysService : IListApiKeysService
{
    private const int DEFAULT_LIMIT = 50;
    private const int MAX_LIMIT = 1000;

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
        // A non-positive limit means "unspecified" and falls back to the default page size; an
        // over-max limit is clamped down to the maximum rather than silently reset to the default.
        limit = limit <= 0 ? DEFAULT_LIMIT : Math.Min(limit, MAX_LIMIT);

        if (offset < 0)
        {
            offset = 0;
        }

        (IReadOnlyList<ApiKey> keys, int totalCount) = await _unitOfWork.ApiKeys.GetPageAsync(offset, limit, cancellationToken);

        var items = keys.Select(ApiKeyMetadataMapper.ToMetadata).ToList();

        return new ListApiKeysResult(items, totalCount, limit, offset);
    }
}
