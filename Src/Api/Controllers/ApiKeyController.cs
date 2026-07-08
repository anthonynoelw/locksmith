namespace Api.Controllers;

using Api.Requests;
using Api.Responses;
using Application.Commands;
using Application.Interfaces.Services;
using Domain;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>Manages API key lifecycle operations.</summary>
[ApiController]
[Route("api/v{version:apiVersion}/api-keys")]
public sealed class ApiKeyController : Controller
{
    private readonly ICreateApiKeyService _createApiKeyService;
    private readonly IListApiKeysService _listApiKeysService;
    private readonly IGetApiKeyByIdService _getApiKeyByIdService;
    private readonly IValidateApiKeySecretService _validateApiKeySecretService;
    private readonly IRetrieveSecretService _retrieveSecretService;
    private readonly IDeleteApiKeyService _deleteApiKeyService;
    private readonly IRotateApiKeyService _rotateApiKeyService;

    /// <summary>Initializes a new instance of the <see cref="ApiKeyController"/> class.</summary>
    /// <param name="createApiKeyService">Service that creates new API keys.</param>
    /// <param name="listApiKeysService">Service that lists API keys with pagination.</param>
    /// <param name="getApiKeyByIdService">Service that retrieves an API key by its ID.</param>
    /// <param name="validateApiKeySecretService">Service that validates an API key secret.</param>
    /// <param name="retrieveSecretService">Service that retrieves and decrypts an API key secret.</param>
    /// <param name="deleteApiKeyService">Service that deletes an API key secret.</param>
    /// <param name="rotateApiKeyService">Service that rotates an API key secret.</param>
    public ApiKeyController(
        ICreateApiKeyService createApiKeyService,
        IListApiKeysService listApiKeysService,
        IGetApiKeyByIdService getApiKeyByIdService,
        IValidateApiKeySecretService validateApiKeySecretService,
        IRetrieveSecretService retrieveSecretService,
        IDeleteApiKeyService deleteApiKeyService,
        IRotateApiKeyService rotateApiKeyService)
    {
        _createApiKeyService = createApiKeyService;
        _listApiKeysService = listApiKeysService;
        _getApiKeyByIdService = getApiKeyByIdService;
        _validateApiKeySecretService = validateApiKeySecretService;
        _retrieveSecretService = retrieveSecretService;
        _deleteApiKeyService = deleteApiKeyService;
        _rotateApiKeyService = rotateApiKeyService;
    }

    /// <summary>Creates a new API key.</summary>
    /// <remarks>
    /// The <c>secret</c> and <c>idempotencyKey</c> fields in the response are returned only once.
    /// Store them securely — they cannot be retrieved again after this call.
    /// </remarks>
    /// <param name="request">The key creation parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>201 Created with the new key ID, plaintext secret, and idempotency key.</returns>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create(
        [FromBody] CreateApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        // CreatedBy is persisted and echoed back in metadata responses, so it must never carry the
        // bearer token itself — a constant caller identity is recorded instead.
        CreateApiKeyResult result = await _createApiKeyService.ExecuteAsync(
            new CreateApiKeyCommand(
                WellKnown.CallerIdentities.API_CLIENT,
                request.ExpiresAt,
                request.Actions),
            cancellationToken);

        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
        Response.Headers.Pragma = "no-cache";

        return Created(
            $"/api/v1/api-keys/{result.ApiKeyId}",
            new CreateApiKeyResponse(result.ApiKeyId, result.PlaintextSecret, result.IdempotencyKey));
    }

    /// <summary>Lists all API keys with pagination.</summary>
    /// <param name="limit">Maximum number of keys to return (default 50, max 1000).</param>
    /// <param name="offset">Number of keys to skip (default 0).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK with paginated list of API key metadata.</returns>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> List(
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        ListApiKeysResult result = await _listApiKeysService.ExecuteAsync(limit, offset, cancellationToken);

        var items = result.Keys.Select(MapToMetadataResponse).ToList();

        return Ok(new ListApiKeysResponse(items, result.Total, result.Limit, result.Offset));
    }

    /// <summary>Gets an API key by its ID.</summary>
    /// <param name="id">The API key identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK with the key metadata, or 404 Not Found.</returns>
    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        ApiKey apiKey = await _getApiKeyByIdService.ExecuteAsync(id, cancellationToken);

        return Ok(MapToMetadataResponse(apiKey));
    }

    /// <summary>Retrieves and decrypts an API key secret using its idempotency key.</summary>
    /// <param name="request">The idempotency key for decryption.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK with the decrypted secret, or 404 Not Found if idempotency key is invalid.</returns>
    [HttpPost("retrieve-secret")]
    [Authorize]
    public async Task<IActionResult> RetrieveSecret(
        [FromBody] RetrieveSecretRequest request,
        CancellationToken cancellationToken)
    {
        RetrieveSecretResult result = await _retrieveSecretService.ExecuteAsync(
            request.IdempotencyKey,
            cancellationToken);

        return Ok(new RetrieveSecretResponse(result.ApiKeyId, result.Secret));
    }

    /// <summary>Validates an API key secret and returns its current status.</summary>
    /// <param name="request">The secret to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK with validation result and current status, or 404 Not Found if secret is invalid.</returns>
    [HttpPost("validate")]
    [Authorize]
    public async Task<IActionResult> Validate(
        [FromBody] ValidateApiKeySecretRequest request,
        CancellationToken cancellationToken)
    {
        ValidateApiKeySecretResult result = await _validateApiKeySecretService.ExecuteAsync(
            request.Secret,
            cancellationToken);

        return Ok(new ValidateApiKeySecretResponse(result.ApiKeyId, result.IsValid));
    }

    /// <summary>Rotates an API key: the current key is deleted and a new one is issued with the same granted actions.</summary>
    /// <param name="request">The idempotency key that identifies the API key to rotate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>201 Created with the new key ID, plaintext secret, and idempotency key, or 404 Not Found if the idempotency key is invalid.</returns>
    [HttpPost("rotate")]
    [Authorize]
    public async Task<IActionResult> Rotate(
        [FromBody] UpdateApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        CreateApiKeyResult result = await _rotateApiKeyService.ExecuteAsync(request.IdempotencyKey, cancellationToken);

        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
        Response.Headers.Pragma = "no-cache";

        return Created(
            $"/api/v1/api-keys/{result.ApiKeyId}",
            new CreateApiKeyResponse(result.ApiKeyId, result.PlaintextSecret, result.IdempotencyKey));
    }

    /// <summary>Deletes an API key identified by its idempotency key.</summary>
    /// <param name="request">The idempotency key that identifies the API key to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>204 No Content on success, or 404 Not Found if the idempotency key is invalid.</returns>
    [HttpDelete]
    [Authorize]
    public async Task<IActionResult> Delete(
        [FromBody] UpdateApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        await _deleteApiKeyService.ExecuteAsync(request.IdempotencyKey, cancellationToken);

        return NoContent();
    }

    private static ApiKeyMetadataResponse MapToMetadataResponse(ApiKey apiKey)
    {
        var currentStatus = apiKey.Statuses
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefault();

        var statusString = currentStatus?.Status.ToString() ?? ApiKeyStatusEnum.Inactive.ToString();
        var actionStrings = apiKey.Actions
            .Where(a => a.DeletedAt == null)
            .Select(a => a.Action.ToString())
            .ToList();

        string maskedHash = MaskSecretHash(apiKey.SecretHash);

        return new ApiKeyMetadataResponse(
            apiKey.Id,
            maskedHash,
            apiKey.CreatedAt,
            apiKey.CreatedBy,
            apiKey.ExpiresAt,
            statusString,
            actionStrings);
    }

    private static string MaskSecretHash(string secretHash)
    {
        if (secretHash.Length <= 4)
        {
            return "****";
        }

        return $"****...{secretHash[^4..]}";
    }
}
