namespace Api.RateLimiting;

using System.Globalization;
using System.Reflection;
using Application.Interfaces.Services;
using Application.Settings;
using Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

/// <summary>
/// Action filter that enforces per-API-key rate limiting. Runs after model binding, so it can read
/// the target key from either a route value or the bound request body. Emits <c>X-RateLimit-*</c>
/// headers on every rate-limited response and short-circuits with an RFC 9457 <c>429</c> when the
/// quota is exceeded.
/// </summary>
public sealed class RateLimitFilter : IAsyncActionFilter
{
    private const string PROBLEM_TYPE_429 = "https://tools.ietf.org/html/rfc6585#section-4";

    private readonly IRateLimiter _rateLimiter;
    private readonly ICryptoService _cryptoService;
    private readonly RateLimitSettings _settings;
    private readonly RateLimitKeySource _source;
    private readonly string _keyName;

    /// <summary>Initializes a new instance of the <see cref="RateLimitFilter"/> class.</summary>
    /// <param name="rateLimiter">The rate limiter backend.</param>
    /// <param name="cryptoService">Hashes body-sourced credentials into stable partition keys.</param>
    /// <param name="settings">The rate-limit configuration.</param>
    /// <param name="source">Where the partition key is read from.</param>
    /// <param name="keyName">The route value name or request-body property name that holds the key.</param>
    public RateLimitFilter(
        IRateLimiter rateLimiter,
        ICryptoService cryptoService,
        IOptions<RateLimitSettings> settings,
        RateLimitKeySource source,
        string keyName)
    {
        _rateLimiter = rateLimiter;
        _cryptoService = cryptoService;
        _settings = settings.Value;
        _source = source;
        _keyName = keyName;
    }

    /// <inheritdoc/>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        string? partitionKey = _settings.Enabled ? ResolvePartitionKey(context) : null;

        if (partitionKey is null)
        {
            // Rate limiting disabled, or the target key could not be determined — let the request through.
            await next();
            return;
        }

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

    private string? ResolvePartitionKey(ActionExecutingContext context)
    {
        return _source switch
        {
            RateLimitKeySource.Route => ResolveFromRoute(context),
            RateLimitKeySource.Body => ResolveFromBody(context),
            _ => null,
        };
    }

    private string? ResolveFromRoute(ActionExecutingContext context)
    {
        if (context.ActionArguments.TryGetValue(_keyName, out object? value) && value is not null)
        {
            return value.ToString();
        }

        return null;
    }

    private string? ResolveFromBody(ActionExecutingContext context)
    {
        foreach (object? argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            PropertyInfo? property = argument.GetType()
                .GetProperty(_keyName, BindingFlags.Public | BindingFlags.Instance);

            if (property is null || property.PropertyType != typeof(string))
            {
                continue;
            }

            var raw = (string?)property.GetValue(argument);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                // Hash so the bucket matches the DB lookup hash and the raw credential never leaves the process.
                return _cryptoService.HashForLookup(raw);
            }
        }

        return null;
    }
}
