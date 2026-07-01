namespace Application.Services.Status;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces;
using Application.Interfaces.Services.Status;
using Domain.Models;

/// <summary>
/// Retrieves the status history of an API key.
/// </summary>
public class GetApiKeyStatusHistoryService : IGetApiKeyStatusHistoryService
{
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetApiKeyStatusHistoryService"/> class.
    /// </summary>
    /// <param name="unitOfWork">Unit of work for repository access.</param>
    public GetApiKeyStatusHistoryService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Gets the full status history of an API key, ordered as returned by the repository.
    /// </summary>
    /// <param name="id">The ID of the API key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>All recorded statuses for the API key.</returns>
    public Task<IReadOnlyList<ApiKeyStatus>> ExecuteAsync(Guid id, CancellationToken cancellationToken = default)
        => _unitOfWork.ApiKeyStatuses.GetByApiKeyIdAsync(id, cancellationToken);
}
