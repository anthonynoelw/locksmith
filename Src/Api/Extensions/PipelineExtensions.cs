namespace Api.Extensions;

using Domain;

using HealthChecks.UI.Client;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;

using Serilog;

/// <summary>
/// Extension methods for configuring the ASP.NET Core middleware pipeline on <see cref="WebApplication"/>.
/// </summary>
internal static class PipelineExtensions
{
    /// <summary>
    /// Configures the full request pipeline: logging, exception handling, OpenAPI,
    /// HTTPS redirection, authorization, MVC controllers, and health check endpoints.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The same <paramref name="app"/> instance for chaining.</returns>
    internal static WebApplication UseApiPipeline(this WebApplication app)
    {
        // Must precede UseExceptionHandler: wraps the full pipeline so Serilog
        // captures the final status code for exception-mapped 5xx responses.
        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
            };
        });

        app.UseExceptionHandler();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();

        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            // Liveness: always Healthy while the process is running — no dependency checks.
            Predicate = _ => false,
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            // Readiness: runs only checks tagged "ready" (EF Core, Redis, etc.).
            Predicate = check => check.Tags.Contains(WellKnown.HealthCheckTags.READY),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
        });

        return app;
    }
}
