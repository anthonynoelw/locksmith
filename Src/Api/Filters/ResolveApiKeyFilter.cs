namespace Api.Filters;

using Application.Interfaces.Services;
using Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

/// <summary>
/// Requires the <c>X-Api-Key</c> header on read endpoints, resolves it to an API key identity, and
/// stashes that identity on the request. This is the single place the caller's API key is turned into
/// a resource identity, and the anchor the future per-API-key rate-limit policy will partition on.
/// </summary>
public sealed class ResolveApiKeyFilter : IAsyncActionFilter
{
    private readonly IGetApiKeyBySecretService _getApiKeyBySecretService;
    private readonly ProblemDetailsFactory _problemDetailsFactory;

    /// <summary>Initializes a new instance of the <see cref="ResolveApiKeyFilter"/> class.</summary>
    /// <param name="getApiKeyBySecretService">Service that resolves an API key identity from its secret.</param>
    /// <param name="problemDetailsFactory">Factory for building RFC 9457 problem responses.</param>
    public ResolveApiKeyFilter(
        IGetApiKeyBySecretService getApiKeyBySecretService,
        ProblemDetailsFactory problemDetailsFactory)
    {
        _getApiKeyBySecretService = getApiKeyBySecretService;
        _problemDetailsFactory = problemDetailsFactory;
    }

    /// <inheritdoc/>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        HttpContext httpContext = context.HttpContext;

        bool hasHeader = httpContext.Request.Headers.TryGetValue(WellKnown.RequestHeaders.API_KEY, out var headerValues);

        // A duplicated header is ambiguous — StringValues would join the values with a comma and never
        // match a real secret — so reject it outright rather than silently mangling the credential.
        if (!hasHeader || headerValues.Count != 1 || string.IsNullOrWhiteSpace(headerValues[0]))
        {
            ProblemDetails problem = _problemDetailsFactory.CreateProblemDetails(
                httpContext,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Missing API key",
                detail: $"Exactly one '{WellKnown.RequestHeaders.API_KEY}' header is required.");

            context.Result = new BadRequestObjectResult(problem);
            return;
        }

        // Count == 1 is guaranteed above, so ToString() yields the single value (no comma joining).
        // A miss surfaces as NotFoundException -> 404 via GlobalExceptionHandler.
        Guid apiKeyId = await _getApiKeyBySecretService.ExecuteAsync(
            headerValues.ToString(),
            httpContext.RequestAborted);

        httpContext.Items[WellKnown.HttpContextItems.RESOLVED_API_KEY_ID] = apiKeyId;

        await next();
    }
}
