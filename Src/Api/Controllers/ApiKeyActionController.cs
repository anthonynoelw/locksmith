namespace Api.Controllers;

using System.Collections.Generic;
using System.Linq;
using Api.Requests;
using Api.Responses;
using Application.Interfaces.Services.Actions;
using Domain;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Manages the action permissions granted to an API key.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/api-keys/{keyId:guid}/actions")]
public sealed class ApiKeyActionController : Controller
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
    /// Lists the currently granted actions of an API key.
    /// </summary>
    /// <param name="keyId">The ID of the API key.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>200 OK with the active actions, or 404 Not Found if the key does not exist.</returns>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> List(Guid keyId, CancellationToken ct)
    {
        IReadOnlyList<ApiKeyAction> result = await _listApiKeyActionsService.ExecuteAsync(keyId, ct);

        return Ok(result.Select(MapToResponse).ToList());
    }

    /// <summary>
    /// Replaces the full action set of an API key, revoking removed actions and granting added ones.
    /// </summary>
    /// <param name="keyId">The ID of the API key.</param>
    /// <param name="request">The desired set of granted actions.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// 200 OK with the resulting active actions, 404 Not Found if the key does not exist,
    /// or 422 Unprocessable Entity when the request contains an undefined action value.
    /// </returns>
    [HttpPut]
    [Authorize]
    public async Task<IActionResult> Replace(
        Guid keyId,
        [FromBody] UpdateApiKeyActionsRequest request,
        CancellationToken ct)
    {
        ValidateActions(request.Actions);

        IReadOnlyList<ApiKeyAction> result = await _replaceApiKeyActionsService.ExecuteAsync(
            keyId,
            request.Actions,
            WellKnown.CallerIdentities.API_CLIENT,
            ct);

        return Ok(result.Select(MapToResponse).ToList());
    }

    /// <summary>
    /// Grants a single action to an API key.
    /// </summary>
    /// <param name="keyId">The ID of the API key.</param>
    /// <param name="actionName">The name of the action to grant.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// 201 Created with the granted action, 404 Not Found if the key does not exist,
    /// 409 Conflict if the action is already granted, or 422 Unprocessable Entity on an invalid action name.
    /// </returns>
    [HttpPost("{actionName}")]
    [Authorize]
    public async Task<IActionResult> Grant(Guid keyId, string actionName, CancellationToken ct)
    {
        ApiKeyActionEnum parsed = ParseAction(actionName);

        ApiKeyAction result = await _grantApiKeyActionService.ExecuteAsync(
            keyId,
            parsed,
            WellKnown.CallerIdentities.API_CLIENT,
            ct);

        return Created(
            $"/api/v1/api-keys/{keyId}/actions",
            MapToResponse(result));
    }

    /// <summary>
    /// Revokes a single action from an API key.
    /// </summary>
    /// <param name="keyId">The ID of the API key.</param>
    /// <param name="actionName">The name of the action to revoke.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// 204 No Content on success, 404 Not Found if the key does not exist or the action is not granted,
    /// or 422 Unprocessable Entity on an invalid action name.
    /// </returns>
    [HttpDelete("{actionName}")]
    [Authorize]
    public async Task<IActionResult> Revoke(Guid keyId, string actionName, CancellationToken ct)
    {
        ApiKeyActionEnum parsed = ParseAction(actionName);

        await _revokeApiKeyActionService.ExecuteAsync(keyId, parsed, ct);

        return NoContent();
    }

    private static ApiKeyActionEnum ParseAction(string actionName)
    {
        // Route values bypass model validation, so an unknown action name must be rejected here.
        // Enum.TryParse is deliberately avoided: it also accepts numeric strings ("3") and
        // comma-separated name lists ("Write,Delete" ORs to 3 == Execute), which would let a caller
        // grant an action they never named. Only exact, case-insensitive name matches are accepted.
        // ValidationException keeps the failure mode mapped to 422 via GlobalExceptionHandler.
        foreach (ApiKeyActionEnum action in Enum.GetValues<ApiKeyActionEnum>())
        {
            if (string.Equals(action.ToString(), actionName, StringComparison.OrdinalIgnoreCase))
            {
                return action;
            }
        }

        throw new ValidationException(
            "Validation failed.",
            new Dictionary<string, string[]> { { "Action", new[] { $"'{actionName}' is not a valid action." } } });
    }

    private static void ValidateActions(IReadOnlyList<ApiKeyActionEnum> actions)
    {
        // JSON enum binding accepts any integer, so undefined values (e.g. 42) must be rejected
        // here before they are persisted as granted permissions.
        string[] invalid = actions
            .Where(a => !Enum.IsDefined(a))
            .Select(a => $"'{(int)a}' is not a valid action.")
            .ToArray();

        if (invalid.Length > 0)
        {
            throw new ValidationException(
                "Validation failed.",
                new Dictionary<string, string[]> { { "Actions", invalid } });
        }
    }

    private static ApiKeyActionResponse MapToResponse(ApiKeyAction action)
        => new (action.Id, action.Action.ToString(), action.CreatedAt);
}
