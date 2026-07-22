namespace Api.Controllers.V1;

using System.Collections.Generic;
using System.Linq;
using Api.Extensions;
using Api.Filters;
using Api.Requests;
using Api.Responses;
using Application.Interfaces.Services.Actions;
using Asp.Versioning;
using Domain;
using Domain.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Manages the action permissions granted to an API key.
/// </summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/api-key")]
public sealed class ApiKeyActionController : Api.Controllers.Controller
{
    private readonly IListApiKeyActionsService _listApiKeyActionsService;
    private readonly IReplaceApiKeyActionsService _replaceApiKeyActionsService;
    private readonly IGrantApiKeyActionService _grantApiKeyActionService;
    private readonly IRevokeApiKeyActionService _revokeApiKeyActionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiKeyActionController"/> class.
    /// </summary>
    /// <param name="listApiKeyActionsService">The service to list the active actions of an API key.</param>
    /// <param name="replaceApiKeyActionsService">The service to replace the full action set of an API key.</param>
    /// <param name="grantApiKeyActionService">The service to grant a single action to an API key.</param>
    /// <param name="revokeApiKeyActionService">The service to revoke a single action from an API key.</param>
    public ApiKeyActionController(
        IListApiKeyActionsService listApiKeyActionsService,
        IReplaceApiKeyActionsService replaceApiKeyActionsService,
        IGrantApiKeyActionService grantApiKeyActionService,
        IRevokeApiKeyActionService revokeApiKeyActionService)
    {
        _listApiKeyActionsService = listApiKeyActionsService;
        _replaceApiKeyActionsService = replaceApiKeyActionsService;
        _grantApiKeyActionService = grantApiKeyActionService;
        _revokeApiKeyActionService = revokeApiKeyActionService;
    }

    /// <summary>
    /// Lists the currently granted actions of the API key identified by the <c>X-Api-Key</c> header.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>200 OK with the active actions, or 404 Not Found when the secret is unknown.</returns>
    [HttpGet("actions")]
    [ServiceFilter(typeof(ResolveApiKeyFilter))]
    [ServiceFilter(typeof(RateLimitFilter), Order = 1)]
    [Cacheable]
    [ProducesResponseType(typeof(IReadOnlyList<ApiKeyActionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ApiKeyActionResponse>>> List(CancellationToken cancellationToken)
    {
        IReadOnlyList<ApiKeyAction> result = await _listApiKeyActionsService.ExecuteAsync(
            HttpContext.GetResolvedApiKeyId(),
            cancellationToken);

        return Ok(result.Select(ToResponse).ToList());
    }

    /// <summary>
    /// Replaces the full action set of an API key, revoking removed actions and granting added ones.
    /// </summary>
    /// <param name="request">The desired set of granted actions and the idempotency key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// 200 OK with the resulting active actions, 404 Not Found if the idempotency key is invalid,
    /// or 422 Unprocessable Entity when the request contains an undefined action value.
    /// </returns>
    [HttpPut("actions")]
    [ProducesResponseType(typeof(IReadOnlyList<ApiKeyActionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ApiKeyActionResponse>>> Replace(
        [FromBody] UpdateApiKeyActionsRequest request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ApiKeyAction> result = await _replaceApiKeyActionsService.ExecuteAsync(
            request.IdempotencyKey,
            request.Actions,
            WellKnown.CallerIdentities.API_CLIENT,
            cancellationToken);

        return Ok(result.Select(ToResponse).ToList());
    }

    /// <summary>
    /// Grants a single action to an API key.
    /// </summary>
    /// <param name="actionName">The name of the action to grant.</param>
    /// <param name="request">The idempotency key that identifies the API key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// 201 Created with the granted action, 404 Not Found if the idempotency key is invalid,
    /// 409 Conflict if the action is already granted, or 422 Unprocessable Entity on an invalid action name.
    /// </returns>
    [HttpPost("actions/{actionName}")]
    [ProducesResponseType(typeof(ApiKeyActionResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiKeyActionResponse>> Grant(
        [FromRoute] string actionName,
        [FromBody] GrantApiKeyActionRequest request,
        CancellationToken cancellationToken)
    {
        ApiKeyAction result = await _grantApiKeyActionService.ExecuteAsync(
            request.IdempotencyKey,
            actionName,
            WellKnown.CallerIdentities.API_CLIENT,
            cancellationToken);

        // Grant is identified by the idempotency key, so the created action has no self-addressable URL.
        return Created((string?)null, ToResponse(result));
    }

    /// <summary>
    /// Revokes a single action from an API key.
    /// </summary>
    /// <param name="actionName">The name of the action to revoke.</param>
    /// <param name="request">The idempotency key that identifies the API key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// 204 No Content on success, 404 Not Found if the idempotency key is invalid or the action is not granted,
    /// or 422 Unprocessable Entity on an invalid action name.
    /// </returns>
    [HttpDelete("actions/{actionName}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Revoke(
        [FromRoute] string actionName,
        [FromBody] RevokeApiKeyActionRequest request,
        CancellationToken cancellationToken)
    {
        await _revokeApiKeyActionService.ExecuteAsync(request.IdempotencyKey, actionName, cancellationToken);

        return NoContent();
    }

    private static ApiKeyActionResponse ToResponse(ApiKeyAction action)
        => new (action.Id, action.Action.ToString(), action.CreatedAt);
}
