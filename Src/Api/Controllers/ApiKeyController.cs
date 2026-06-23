namespace Api.Controllers;

using Api.Requests;
using Api.Responses;
using Api.Settings;
using Application.Commands;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

/// <summary>Manages API key lifecycle operations.</summary>
[Route("api/v{version:apiVersion}/api-keys")]
public sealed class ApiKeyController : Controller
{
    private readonly ICreateApiKeyService _createApiKeyService;
    private readonly ApiSettings _apiSettings;

    /// <summary>Initializes a new instance of the <see cref="ApiKeyController"/> class.</summary>
    /// <param name="createApiKeyService">Service that creates new API keys.</param>
    /// <param name="apiSettings">API-level settings providing the caller identity.</param>
    public ApiKeyController(ICreateApiKeyService createApiKeyService, IOptions<ApiSettings> apiSettings)
    {
        _createApiKeyService = createApiKeyService;
        _apiSettings = apiSettings.Value;
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
        CreateApiKeyResult result = await _createApiKeyService.Execute(
            new CreateApiKeyCommand(
                _apiSettings.BearerToken,
                request.ExpiresAt,
                request.Actions),
            cancellationToken);

        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
        Response.Headers.Pragma = "no-cache";

        return Created(
            $"/api/v1/api-keys/{result.ApiKeyId}",
            new CreateApiKeyResponse(result.ApiKeyId, result.PlaintextSecret, result.IdempotencyKey));
    }
}
