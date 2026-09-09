namespace Api.Filters;

using System.Linq;
using Api.Requests;
using Application.Interfaces.Services;
using Application.Settings;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

/// <summary>
/// Action filter that enforces per-API-key rate limiting on <c>idempotencyKey</c>/secret-identified
/// mutation endpoints, which carry no <c>X-Api-Key</c> header for <see cref="ResolveApiKeyFilter"/> to
/// resolve. Partitions on a hash of the bound request body's <see cref="IRateLimitCredential"/> value —
/// the same hash the corresponding lookup uses — so no extra database round trip is needed just to rate
/// limit. Emits the same <c>X-RateLimit-*</c> headers and <c>429</c> body as <see cref="RateLimitFilter"/>.
/// </summary>
/// <remarks>
/// Runs after model binding (all MVC action filters do), so the credential is already available on
/// <see cref="Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext.ActionArguments"/>. The controllers
/// this targets derive from <see cref="Api.Controllers.Controller"/>, whose <c>[ApiController]</c>
/// attribute rejects an invalid model (including a missing/blank credential) with <c>422</c> before any
/// action filter runs, so the credential is guaranteed non-empty here.
/// </remarks>
public sealed class CredentialRateLimitFilter : IAsyncActionFilter
{
    private readonly IRateLimiter _rateLimiter;
    private readonly ICryptoService _cryptoService;
    private readonly RateLimitSettings _settings;

    /// <summary>Initializes a new instance of the <see cref="CredentialRateLimitFilter"/> class.</summary>
    /// <param name="rateLimiter">The rate limiter backend.</param>
    /// <param name="cryptoService">Hashes the request-body credential into a stable partition key.</param>
    /// <param name="settings">The rate-limit configuration.</param>
    public CredentialRateLimitFilter(
        IRateLimiter rateLimiter,
        ICryptoService cryptoService,
        IOptions<RateLimitSettings> settings)
    {
        _rateLimiter = rateLimiter;
        _cryptoService = cryptoService;
        _settings = settings.Value;
    }

    /// <inheritdoc/>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!_settings.Enabled)
        {
            await next();
            return;
        }

        IRateLimitCredential credentialRequest = context.ActionArguments.Values
            .OfType<IRateLimitCredential>()
            .First();

        string partitionKey = _cryptoService.HashForLookup(credentialRequest.RateLimitCredential);

        RateLimitResult result = await _rateLimiter.AcquireAsync(partitionKey, context.HttpContext.RequestAborted);

        RateLimitResponseWriter.ApplyQuotaHeaders(context.HttpContext.Response, result);

        if (!result.IsAllowed)
        {
            context.Result = RateLimitResponseWriter.BuildTooManyRequestsResult(context.HttpContext, result);
            return;
        }

        await next();
    }
}
