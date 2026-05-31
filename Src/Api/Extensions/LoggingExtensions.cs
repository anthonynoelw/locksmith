namespace Api.Extensions;

using Serilog;

/// <summary>
/// Extension methods for configuring Serilog structured logging on <see cref="IHostApplicationBuilder"/>.
/// </summary>
internal static class LoggingExtensions
{
    /// <summary>
    /// Replaces the default logging provider with Serilog.
    /// Sink selection and log levels are driven entirely by <c>appsettings.json</c>
    /// with per-environment overrides, so no values are hard-coded here.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The same <paramref name="builder"/> instance for chaining.</returns>
    internal static IHostApplicationBuilder AddSerilogLogging(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSerilog(
            (services, config) => config
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentName());

        return builder;
    }
}
