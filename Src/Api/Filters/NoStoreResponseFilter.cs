namespace Api.Filters;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;

/// <summary>
/// Sets no-store cache directives on every controller response. The API deals exclusively in API key
/// material and its metadata, none of which may be cached or stored by clients or intermediaries.
/// </summary>
public sealed class NoStoreResponseFilter : IAlwaysRunResultFilter
{
    private const string NO_STORE = "no-store, no-cache, must-revalidate, max-age=0";

    /// <summary>
    /// Applies the no-store cache directives to the supplied response headers. Shared with the global
    /// exception handler so that error responses (produced outside the MVC result pipeline) are covered too.
    /// </summary>
    /// <param name="headers">The response headers to stamp.</param>
    public static void ApplyNoStore(IHeaderDictionary headers)
    {
        headers[HeaderNames.CacheControl] = NO_STORE;
        headers[HeaderNames.Pragma] = "no-cache";
    }

    /// <inheritdoc/>
    public void OnResultExecuting(ResultExecutingContext context) =>
        ApplyNoStore(context.HttpContext.Response.Headers);

    /// <inheritdoc/>
    public void OnResultExecuted(ResultExecutedContext context)
    {
        // No action required after the result executes.
    }
}
