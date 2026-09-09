namespace Api.Filters;

using System.Globalization;
using Application.Interfaces.Services;
using Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Builds the <c>X-RateLimit-*</c> headers and the RFC 9457 <c>429</c> problem response shared by every
/// rate-limit action filter, so <see cref="RateLimitFilter"/> and <see cref="CredentialRateLimitFilter"/>
/// render identical output regardless of how they partition the request.
/// </summary>
internal static class RateLimitResponseWriter
{
    private const string PROBLEM_TYPE_429 = "https://tools.ietf.org/html/rfc6585#section-4";

    /// <summary>Sets the limit/remaining/reset headers on every rate-limited response.</summary>
    /// <param name="response">The response to write headers on.</param>
    /// <param name="result">The outcome of the rate-limit acquisition attempt.</param>
    public static void ApplyQuotaHeaders(HttpResponse response, RateLimitResult result)
    {
        response.Headers[WellKnown.RateLimitHeaders.LIMIT] =
            result.Limit.ToString(CultureInfo.InvariantCulture);
        response.Headers[WellKnown.RateLimitHeaders.REMAINING] =
            result.Remaining.ToString(CultureInfo.InvariantCulture);
        response.Headers[WellKnown.RateLimitHeaders.RESET] =
            result.ResetAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Builds the <c>429 Too Many Requests</c> result, including the <c>Retry-After</c> header.</summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="result">The outcome of the rate-limit acquisition attempt.</param>
    /// <returns>An RFC 9457 ProblemDetails result carrying a <c>429</c> status.</returns>
    public static ObjectResult BuildTooManyRequestsResult(HttpContext httpContext, RateLimitResult result)
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
