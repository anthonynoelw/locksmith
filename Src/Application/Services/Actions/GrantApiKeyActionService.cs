namespace Application.Services.Actions;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces;
using Application.Interfaces.Services.Actions;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Models;

/// <summary>
/// Grants a single action to an API key.
/// </summary>
public sealed class GrantApiKeyActionService : IGrantApiKeyActionService
{
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="GrantApiKeyActionService"/> class.
    /// </summary>
    /// <param name="unitOfWork">Unit of work for repository access.</param>
    public GrantApiKeyActionService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Grants an action to an API key by appending a new action row.
    /// </summary>
    /// <param name="apiKeyId">The ID of the API key.</param>
    /// <param name="action">The action to grant.</param>
    /// <param name="createdBy">The identity of the caller granting the action.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The newly granted action.</returns>
    /// <exception cref="NotFoundException">Thrown when no API key exists for the given <paramref name="apiKeyId"/>.</exception>
    /// <exception cref="ConflictException">Thrown when the action is already actively granted to the API key.</exception>
    public async Task<ApiKeyAction> ExecuteAsync(
        Guid apiKeyId,
        ApiKeyActionEnum action,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        ApiKey apiKey = await _unitOfWork.ApiKeys.GetByIdAsync(apiKeyId, cancellationToken)
            ?? throw new NotFoundException($"API key with ID {apiKeyId} not found.");

        IReadOnlyList<ApiKeyAction> activeActions = await _unitOfWork.ApiKeyActions.GetActiveByApiKeyIdAsync(
            apiKeyId,
            cancellationToken);

        if (activeActions.Any(a => a.Action == action))
        {
            throw new ConflictException($"The Action: {action} is already granted to the ApiKey");
        }

        var apiKeyAction = new ApiKeyAction
        {
            Id = Guid.NewGuid(),
            ApiKeyId = apiKeyId,
            Action = action,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            DeletedAt = null,
            ApiKey = apiKey,
        };

        await _unitOfWork.ApiKeyActions.AddAsync(apiKeyAction, cancellationToken);

        return apiKeyAction;
    }
}
