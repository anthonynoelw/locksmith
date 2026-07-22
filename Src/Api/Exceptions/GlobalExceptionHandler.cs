namespace Api.Exceptions;

using Api.Filters;

using Domain.Exceptions;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Catches all unhandled exceptions and writes an RFC 9457-compliant ProblemDetails response.
/// Always returns <see langword="true"/> so that no exception escapes to the default error page.
/// </summary>
/// <remarks>
/// <para>
/// In <b>Development</b>, the <c>detail</c> field contains the original
/// <see cref="Exception.Message"/>. In all other environments the detail for
/// unexpected (500) errors is replaced with a generic message to avoid leaking
/// internal information.
/// </para>
/// <para>
/// Domain exceptions that represent expected business conditions (404, 409, 422)
/// always include the message as <c>detail</c> regardless of environment, because
/// these are intentional user-facing messages written by the application team.
/// </para>
/// </remarks>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IWebHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    private const string UNEXPECTED_ERROR_DETAIL = "An unexpected error occurred. Please try again later.";

    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(
            exception,
            "Unhandled exception on {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        ProblemDetails problemDetails = exception switch
        {
            NotFoundException notFound => BuildProblemDetails(
                StatusCodes.Status404NotFound,
                "Not Found",
                "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                notFound.Message,
                httpContext),

            ValidationException validation => BuildValidationProblemDetails(
                validation,
                httpContext),

            ConflictException conflict => BuildProblemDetails(
                StatusCodes.Status409Conflict,
                "Conflict",
                "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                conflict.Message,
                httpContext),

            DecryptionFailedException decryption => BuildProblemDetails(
                StatusCodes.Status422UnprocessableEntity,
                "Unprocessable Entity",
                "https://tools.ietf.org/html/rfc9110#section-15.5.21",
                decryption.Message,
                httpContext),

            _ => BuildProblemDetails(
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                ResolveUnexpectedDetail(exception),
                httpContext),
        };

        httpContext.Response.StatusCode = problemDetails.Status!.Value;

        // Error responses are produced in middleware, outside the MVC result pipeline, so the
        // ResponseCacheControlFilter never runs for them. Stamp the same directives here so errors are
        // never cached, even for endpoints whose successful responses are.
        ResponseCacheControlFilter.ApplyNoStore(httpContext.Response.Headers);

        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception,
        });

        return true;
    }

    private static ProblemDetails BuildProblemDetails(
        int status,
        string title,
        string type,
        string detail,
        HttpContext httpContext)
    {
        return new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = type,
            Detail = detail,
            Instance = httpContext.Request.Path,
        };
    }

    private static ValidationProblemDetails BuildValidationProblemDetails(
        ValidationException exception,
        HttpContext httpContext)
    {
        var details = new ValidationProblemDetails(
            exception.Errors.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value))
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "Unprocessable Entity",
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.21",
            Detail = exception.Message,
            Instance = httpContext.Request.Path,
        };

        return details;
    }

    private string ResolveUnexpectedDetail(Exception exception)
    {
        return environment.IsDevelopment()
            ? exception.Message
            : UNEXPECTED_ERROR_DETAIL;
    }
}
