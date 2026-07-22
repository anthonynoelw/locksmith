namespace Api.Filters;

using Api.Extensions;
using Application.Interfaces.Services;
using Application.Settings;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

/// <summary>
/// Action filter that enforces per-API-key rate limiting, partitioned on the identity
/// <see cref="ResolveApiKeyFilter"/> resolved earlier in the pipeline. Emits <c>X-RateLimit-*</c>
/// headers on every rate-limited response and short-circuits with an RFC 9457 <c>429</c> when the
/// quota is exceeded.
/// </summary>
/// <remarks>
/// Must run after <see cref="ResolveApiKeyFilter"/> on the same action (see its <c>Order</c>).
/// </remarks>
public sealed class RateLimitFilter : IAsyncActionFilter
{
    private readonly IRateLimiter _rateLimiter;
    private readonly RateLimitSettings _settings;

    /// <summary>Initializes a new instance of the <see cref="RateLimitFilter"/> class.</summary>
    /// <param name="rateLimiter">The rate limiter backend.</param>
    /// <param name="settings">The rate-limit configuration.</param>
    public RateLimitFilter(IRateLimiter rateLimiter, IOptions<RateLimitSettings> settings)
    {
        _rateLimiter = rateLimiter;
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

        string partitionKey = context.HttpContext.GetResolvedApiKeyId().ToString();

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
