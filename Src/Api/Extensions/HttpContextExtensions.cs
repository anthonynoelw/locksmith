namespace Api.Extensions;

using Domain;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Helpers for reading per-request values that middleware and filters stash on <see cref="HttpContext"/>.
/// </summary>
internal static class HttpContextExtensions
{
    /// <summary>
    /// Gets the identifier of the API key resolved from the <c>X-Api-Key</c> header by
    /// <see cref="Filters.ResolveApiKeyFilter"/>. Only present on endpoints that carry the filter.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <returns>The resolved API key identifier.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no API key was resolved for the request, indicating the calling endpoint was not
    /// guarded by <see cref="Filters.ResolveApiKeyFilter"/>.
    /// </exception>
    public static Guid GetResolvedApiKeyId(this HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue(WellKnown.HttpContextItems.RESOLVED_API_KEY_ID, out object? value)
            && value is Guid id)
        {
            return id;
        }

        throw new InvalidOperationException(
            "No resolved API key on the request. Ensure the endpoint is guarded by ResolveApiKeyFilter.");
    }
}
