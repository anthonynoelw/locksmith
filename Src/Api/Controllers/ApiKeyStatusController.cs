namespace Api.Controllers;

using System.Collections.Generic;
using System.Linq;
using Api.Requests;
using Api.Responses;
using Application.Interfaces.Services.Status;
using Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Manages the status of an API key and the related secret.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/api-keys/status")]
public sealed class ApiKeyStatusController : Controller
{
    private readonly IGetApiKeyStatusService _getApiKeyStatusService;
    private readonly IGetApiKeyStatusHistoryService _getApiKeyStatusHistoryService;
    private readonly IUpdateApiKeyStatusService _updateApiKeyStatusService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiKeyStatusController"/> class.
    /// </summary>
    /// <param name="getApiKeyStatusService">The service to get the current status of an API key by its ID.</param>
    /// <param name="getApiKeyStatusHistoryService">The service to get the status history of an API key by its ID.</param>
    /// <param name="updateApiKeyStatusService">The service to update an API key Status by its IdempotencyKey.</param>
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
    /// Gets the status of an API key and the related secret.
    /// </summary>
    /// <param name="id">The ID of the API key.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The status of the API key and the related secret.</returns>
    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetCurrentStatus(Guid id, CancellationToken ct)
    {
        ApiKeyStatus result = await _getApiKeyStatusService.ExecuteAsync(id, ct);

        return Ok(new ApiKeyStatusResponse(result.Id, result.Status.ToString(), result.CreatedAt));
    }

    /// <summary>
    /// Gets all statuses for an API key.
    /// </summary>
    /// <param name="id">The ID of the API key.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The statuses for the API key.</returns>
    [HttpGet("{id:guid}/history")]
    [Authorize]
    public async Task<IActionResult> List(Guid id, CancellationToken ct)
    {
        IReadOnlyList<ApiKeyStatus> result = await _getApiKeyStatusHistoryService.ExecuteAsync(id, ct);

        var statuses = result
            .Select(s => new ApiKeyStatusHistoryResponse(s.Id, s.Status.ToString(), s.CreatedAt, s.DeletedAt))
            .ToList();

        return Ok(statuses);
    }

    /// <summary>
    /// Updates the status of an API key identified by its idempotency key.
    /// </summary>
    /// <param name="request">The idempotency key and new status.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>200 OK once the status has been updated.</returns>
    [HttpPatch("update")]
    [Authorize]
    public async Task<IActionResult> Update([FromBody] UpdateApiKeyStatusRequest request, CancellationToken ct)
    {
        // Status is [Required] and validated by ApiBehaviorOptions before this action runs,
        // so a null value here can only mean model validation was bypassed.
        await _updateApiKeyStatusService.ExecuteAsync(request.IdempotencyKey, request.Status!.Value, ct);
        return Ok();
    }
}
