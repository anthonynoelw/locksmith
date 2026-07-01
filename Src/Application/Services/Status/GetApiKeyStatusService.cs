namespace Application.Services.Status;

using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces;
using Application.Interfaces.Services.Status;
using Domain.Exceptions;
using Domain.Models;

/// <summary>
/// Retrieves the current status of an API key.
/// </summary>
public class GetApiKeyStatusService : IGetApiKeyStatusService
{
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetApiKeyStatusService"/> class.
    /// </summary>
    /// <param name="unitOfWork">Unit of work for repository access.</param>
    public GetApiKeyStatusService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Gets the current status of an API key.
    /// </summary>
    /// <param name="id">The ID of the API key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current status of the API key.</returns>
    /// <exception cref="NotFoundException">Thrown when no current status exists for the given <paramref name="id"/>.</exception>
    public async Task<ApiKeyStatus> ExecuteAsync(Guid id, CancellationToken cancellationToken = default)
        => await _unitOfWork.ApiKeyStatuses.GetCurrentStatusAsync(id, cancellationToken) ?? throw new NotFoundException($"Api Key Status with ID {id} was not found");
}
