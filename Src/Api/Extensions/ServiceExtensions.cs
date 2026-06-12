namespace Api.Extensions;

using Asp.Versioning;

using Api.Exceptions;
using Api.OpenApi;
using Api.Settings;
using Domain;
using Infrastructure.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Extension methods for registering application services on <see cref="IHostApplicationBuilder"/>.
/// </summary>
internal static class ServiceExtensions
{
    /// <summary>
    /// Registers all API services: controllers, URL-segment API versioning, version-aware
    /// OpenAPI documents, RFC 9457 problem details, the global exception handler, and health checks.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The same <paramref name="builder"/> instance for chaining.</returns>
    internal static IHostApplicationBuilder AddApiServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddControllers();

        builder.Services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddApiExplorer(options =>
            {
                // Formats the version as "v1", "v2", etc. in OpenAPI group names.
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

        // One AddOpenApi call per supported version. The default MapOpenApi() pattern
        // (/openapi/{documentName}.json) serves each document at its versioned URL.
        // When adding a new version, add a corresponding AddOpenApi("v2", ...) call here.
        builder.Services.AddOpenApi("v1", options =>
            options.AddDocumentTransformer<ApiVersionDocumentTransformer>());

        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

        string redisConnectionString = builder.Configuration.GetConnectionString(WellKnown.ConnectionStringKeys.REDIS)
            ?? throw new InvalidOperationException("Connection string 'Redis' not found.");

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>(tags: new[] { WellKnown.HealthCheckTags.READY })
            .AddRedis(redisConnectionString, tags: new[] { WellKnown.HealthCheckTags.READY });

        builder.Services
            .AddOptions<ApiSettings>()
            .BindConfiguration(WellKnown.ConfigSections.API)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return builder;
    }
}
