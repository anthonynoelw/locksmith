namespace Api.Filters;

using System.Globalization;
using Api.Extensions;
using Application.Interfaces.Services;
using Application.Settings;
using Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
    private const string PROBLEM_TYPE_429 = "https://tools.ietf.org/html/rfc6585#section-4";

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

        ApplyQuotaHeaders(context.HttpContext.Response, result);

        if (!result.IsAllowed)
        {
            context.Result = BuildTooManyRequestsResult(context.HttpContext, result);
            return;
        }

        await next();
    }

    private static void ApplyQuotaHeaders(HttpResponse response, RateLimitResult result)
    {
        response.Headers[WellKnown.RateLimitHeaders.LIMIT] =
            result.Limit.ToString(CultureInfo.InvariantCulture);
        response.Headers[WellKnown.RateLimitHeaders.REMAINING] =
            result.Remaining.ToString(CultureInfo.InvariantCulture);
        response.Headers[WellKnown.RateLimitHeaders.RESET] =
            result.ResetAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
    }

    private static ObjectResult BuildTooManyRequestsResult(HttpContext httpContext, RateLimitResult result)
    {
        int retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(result.RetryAfter.TotalSeconds));
        httpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too Many Requests",
            Type = PROBLEM_TYPE_429,
            Detail = "Rate limit exceeded for this API key. Retry after the period indicated by the Retry-After header.",
            Instance = httpContext.Request.Path,
        };

        return new ObjectResult(problemDetails)
        {
            StatusCode = StatusCodes.Status429TooManyRequests,
            ContentTypes = { "application/problem+json" },
        };
    }
}
