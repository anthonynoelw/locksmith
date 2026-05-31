namespace Agent.Extensions;

using Agent.Settings;
using Domain;

/// <summary>
/// Extension methods for registering agent services on <see cref="IHostApplicationBuilder"/>.
/// </summary>
internal static class ServiceExtensions
{
    /// <summary>
    /// Registers all Agent hosted services.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The same <paramref name="builder"/> instance for chaining.</returns>
    internal static IHostApplicationBuilder AddAgentServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHostedService<Worker>();

        builder.Services
            .AddOptions<AgentSettings>()
            .BindConfiguration(WellKnown.ConfigSections.Agent)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return builder;
    }
}
