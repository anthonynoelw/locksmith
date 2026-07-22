namespace Api.Controllers;

using System.Collections.Generic;
using System.Linq;
using Api.Extensions;
using Api.Filters;
using Api.Requests;
using Api.Responses;
using Application.Interfaces.Services.Status;
using Domain;
using Domain.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Manages the status of an API key and the related secret.
/// </summary>
[Route("api/v{version:apiVersion}/api-key")]
public sealed class ApiKeyStatusController : Controller
{
    private readonly IGetApiKeyStatusService _getApiKeyStatusService;
    private readonly IGetApiKeyStatusHistoryService _getApiKeyStatusHistoryService;
    private readonly IUpdateApiKeyStatusService _updateApiKeyStatusService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiKeyStatusController"/> class.
    /// </summary>
    /// <param name="getApiKeyStatusService">The service to get the current status of an API key.</param>
    /// <param name="getApiKeyStatusHistoryService">The service to get the status history of an API key.</param>
    /// <param name="updateApiKeyStatusService">The service to update an API key status by its idempotency key.</param>
    public ApiKeyStatusController(
        IGetApiKeyStatusService getApiKeyStatusService,
        IGetApiKeyStatusHistoryService getApiKeyStatusHistoryService,
        IUpdateApiKeyStatusService updateApiKeyStatusService)
    {
        _getApiKeyStatusService = getApiKeyStatusService;
        _getApiKeyStatusHistoryService = getApiKeyStatusHistoryService;
        _updateApiKeyStatusService = updateApiKeyStatusService;
    }

    /// <summary>
    /// Gets the current status of the API key identified by the <c>X-Api-Key</c> header.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>200 OK with the current status, or 404 Not Found when the secret is unknown.</returns>
    [HttpGet("status")]
    [ServiceFilter(typeof(ResolveApiKeyFilter))]
    [Cacheable]
    [ProducesResponseType(typeof(ApiKeyStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiKeyStatusResponse>> GetCurrentStatus(CancellationToken cancellationToken)
    {
        ApiKeyStatus result = await _getApiKeyStatusService.ExecuteAsync(
            HttpContext.GetResolvedApiKeyId(),
            cancellationToken);

        return Ok(new ApiKeyStatusResponse(result.Id, result.Status.ToString(), result.CreatedAt));
    }

    /// <summary>
    /// Gets the full status history of the API key identified by the <c>X-Api-Key</c> header.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>200 OK with the status history, or 404 Not Found when the secret is unknown.</returns>
    [HttpGet("status/history")]
    [ServiceFilter(typeof(ResolveApiKeyFilter))]
    [Cacheable]
    [ProducesResponseType(typeof(IReadOnlyList<ApiKeyStatusHistoryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ApiKeyStatusHistoryResponse>>> History(CancellationToken cancellationToken)
    {
        IReadOnlyList<ApiKeyStatus> result = await _getApiKeyStatusHistoryService.ExecuteAsync(
            HttpContext.GetResolvedApiKeyId(),
            cancellationToken);

        var statuses = result
            .Select(s => new ApiKeyStatusHistoryResponse(s.Id, s.Status.ToString(), s.CreatedAt, s.DeletedAt))
            .ToList();

        return Ok(statuses);
    }

    /// <summary>
    /// Updates the status of an API key identified by its idempotency key.
    /// </summary>
    /// <param name="request">The idempotency key and new status.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>200 OK once the status has been updated.</returns>
    [HttpPatch("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        [FromBody] UpdateApiKeyStatusRequest request,
        CancellationToken cancellationToken)
    {
        // Status is [Required], so model validation rejects a null value with 422 before this runs.
        await _updateApiKeyStatusService.ExecuteAsync(request.IdempotencyKey, request.Status!.Value, cancellationToken);

        return Ok();
    }
}
