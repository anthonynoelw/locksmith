namespace Application.Services.Actions;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces;
using Application.Interfaces.Services.Actions;
using Domain.Exceptions;
using Domain.Models;

/// <summary>
/// Lists the actions granted to an API key.
/// </summary>
public sealed class ListApiKeyActionsService : IListApiKeyActionsService
{
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListApiKeyActionsService"/> class.
    /// </summary>
    /// <param name="unitOfWork">Unit of work for repository access.</param>
    public ListApiKeyActionsService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Gets all currently granted (non-revoked) actions of an API key.
    /// </summary>
    /// <param name="apiKeyId">The ID of the API key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The active actions granted to the API key.</returns>
    /// <exception cref="NotFoundException">Thrown when no API key exists for the given <paramref name="apiKeyId"/>.</exception>
    public async Task<IReadOnlyList<ApiKeyAction>> ExecuteAsync(
        Guid apiKeyId,
        CancellationToken cancellationToken = default)
    {
        _ = await _unitOfWork.ApiKeys.GetByIdAsync(apiKeyId, cancellationToken)
            ?? throw new NotFoundException($"API key with ID {apiKeyId} not found.");

        return await _unitOfWork.ApiKeyActions.GetActiveByApiKeyIdAsync(apiKeyId, cancellationToken);
    }
}
