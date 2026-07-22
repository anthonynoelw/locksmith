namespace Api.Filters;

using Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;

/// <summary>
/// Stamps every controller response with an explicit cache-control directive: no-store by default, or
/// a short private cache for actions marked <see cref="CacheableAttribute"/>. The API deals
/// exclusively in API key material and its metadata, so no response may leave without an explicit
/// directive one way or the other.
/// </summary>
public sealed class ResponseCacheControlFilter : IAlwaysRunResultFilter
{
    private const string NO_STORE = "no-store, no-cache, must-revalidate, max-age=0";

    /// <summary>
    /// Applies the no-store cache directives to the supplied response headers. Shared with the global
    /// exception handler so that error responses (produced outside the MVC result pipeline, and never
    /// cacheable regardless of the endpoint) are covered too.
    /// </summary>
    /// <param name="headers">The response headers to stamp.</param>
    public static void ApplyNoStore(IHeaderDictionary headers)
    {
        headers[HeaderNames.CacheControl] = NO_STORE;
        headers[HeaderNames.Pragma] = "no-cache";
    }

    /// <inheritdoc/>
    public void OnResultExecuting(ResultExecutingContext context)
    {
        CacheableAttribute? cacheable = IsSuccessResult(context.Result)
            ? context.ActionDescriptor.EndpointMetadata.OfType<CacheableAttribute>().FirstOrDefault()
            : null;

        if (cacheable is null)
        {
            ApplyNoStore(context.HttpContext.Response.Headers);
            return;
        }

        // The response varies per caller: the same URL returns different data depending on which API
        // key's secret is presented, so any cache (including a shared/browser one) must key on it too
        // — otherwise it could serve one caller's data to a different caller entirely.
        context.HttpContext.Response.Headers[HeaderNames.CacheControl] = $"private, max-age={cacheable.MaxAgeSeconds}";
        context.HttpContext.Response.Headers[HeaderNames.Vary] = WellKnown.RequestHeaders.API_KEY;
    }

    /// <inheritdoc/>
    public void OnResultExecuted(ResultExecutedContext context)
    {
        // No action required after the result executes.
    }

    private static bool IsSuccessResult(IActionResult result) => result switch
    {
        ObjectResult objectResult => objectResult.StatusCode is null or >= 200 and < 300,
        StatusCodeResult statusCodeResult => statusCodeResult.StatusCode is >= 200 and < 300,

        // Fail closed: an action result type this filter does not recognize should never be cached.
        _ => false,
    };
}
