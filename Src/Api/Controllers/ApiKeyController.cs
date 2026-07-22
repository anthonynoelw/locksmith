namespace Api.Controllers;

using System.Linq;
using Api.Extensions;
using Api.Filters;
using Api.Requests;
using Api.Responses;
using Application.Commands;
using Application.Interfaces.Services;
using Application.Services;
using Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

/// <summary>Manages API key lifecycle operations.</summary>
[Route("api/v{version:apiVersion}/api-key")]
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
    /// <param name="getApiKeyByIdService">Service that retrieves an API key's metadata by its ID.</param>
    /// <param name="validateApiKeySecretService">Service that validates an API key secret.</param>
    /// <param name="retrieveSecretService">Service that retrieves and decrypts an API key secret.</param>
    /// <param name="deleteApiKeyService">Service that deletes an API key.</param>
    /// <param name="rotateApiKeyService">Service that rotates an API key.</param>
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

    /// <summary>Gets the metadata of the API key identified by the <c>X-Api-Key</c> header.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK with the key metadata, or 404 Not Found when the secret is unknown.</returns>
    [HttpGet]
    [ServiceFilter(typeof(ResolveApiKeyFilter))]
    [Cacheable(WellKnown.CacheDurations.API_KEY_READ_SECONDS)]
    [ProducesResponseType(typeof(ApiKeyMetadataResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiKeyMetadataResponse>> GetCurrent(CancellationToken cancellationToken)
    {
        ApiKeyMetadata metadata = await _getApiKeyByIdService.ExecuteAsync(
            HttpContext.GetResolvedApiKeyId(),
            cancellationToken);

        return Ok(ToResponse(metadata));
    }

    /// <summary>Lists all API keys with pagination.</summary>
    /// <param name="limit">Maximum number of keys to return (default 50, max 1000).</param>
    /// <param name="offset">Number of keys to skip (default 0).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK with a paginated list of API key metadata.</returns>
    [HttpGet("all")]
    [ProducesResponseType(typeof(ListApiKeysResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ListApiKeysResponse>> ListAll(
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        ListApiKeysResult result = await _listApiKeysService.ExecuteAsync(limit, offset, cancellationToken);

        var items = result.Items.Select(ToResponse).ToList();

        return Ok(new ListApiKeysResponse(items, result.Total, result.Limit, result.Offset));
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
    [ProducesResponseType(typeof(CreateApiKeyResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<CreateApiKeyResponse>> Create(
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

        return CreatedAtAction(
            nameof(GetCurrent),
            null,
            new CreateApiKeyResponse(result.ApiKeyId, result.PlaintextSecret, result.IdempotencyKey));
    }

    /// <summary>Retrieves and decrypts an API key secret using its idempotency key.</summary>
    /// <param name="request">The idempotency key for decryption.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK with the decrypted secret, or 404 Not Found if the idempotency key is invalid.</returns>
    [HttpPost("secret")]
    [ProducesResponseType(typeof(RetrieveSecretResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RetrieveSecretResponse>> RetrieveSecret(
        [FromBody] RetrieveSecretRequest request,
        CancellationToken cancellationToken)
    {
        RetrieveSecretResult result = await _retrieveSecretService.ExecuteAsync(
            request.IdempotencyKey,
            cancellationToken);

        return Ok(new RetrieveSecretResponse(result.ApiKeyId, result.Secret));
    }

    /// <summary>Validates an API key secret and returns whether it is currently valid.</summary>
    /// <param name="request">The secret to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK with the validation result, or 404 Not Found if the secret is invalid.</returns>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(ValidateApiKeySecretResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ValidateApiKeySecretResponse>> Validate(
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
    [ProducesResponseType(typeof(CreateApiKeyResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<CreateApiKeyResponse>> Rotate(
        [FromBody] UpdateApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        CreateApiKeyResult result = await _rotateApiKeyService.ExecuteAsync(request.IdempotencyKey, cancellationToken);

        return CreatedAtAction(
            nameof(GetCurrent),
            null,
            new CreateApiKeyResponse(result.ApiKeyId, result.PlaintextSecret, result.IdempotencyKey));
    }

    /// <summary>Deletes an API key identified by its idempotency key.</summary>
    /// <param name="request">The idempotency key that identifies the API key to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>204 No Content on success, or 404 Not Found if the idempotency key is invalid.</returns>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(
        [FromBody] UpdateApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        await _deleteApiKeyService.ExecuteAsync(request.IdempotencyKey, cancellationToken);

        return NoContent();
    }

    private static ApiKeyMetadataResponse ToResponse(ApiKeyMetadata metadata)
        => new (
            metadata.Id,
            metadata.MaskedSecretHash,
            metadata.CreatedAt,
            metadata.CreatedBy,
            metadata.ExpiresAt,
            metadata.Status,
            metadata.Actions);
}
