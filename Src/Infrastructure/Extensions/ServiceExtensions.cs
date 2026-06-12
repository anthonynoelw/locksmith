namespace Infrastructure.Extensions;

using Application.Interfaces;
using Application.Interfaces.Repositories;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for registering Infrastructure layer services on <see cref="IHostApplicationBuilder"/>.
/// </summary>
public static class ServiceExtensions
{
    /// <summary>
    /// Registers the database context, unit of work, and repositories.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The same <paramref name="builder"/> instance for chaining.</returns>
    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.Services.AddInfrastructureServicesInternal(builder.Configuration);
        return builder;
    }

    private static IServiceCollection AddInfrastructureServicesInternal(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IApiKeyRepository>(sp => new ApiKeyRepository(sp.GetRequiredService<AppDbContext>()));
        services.AddScoped<IApiKeyStatusRepository>(sp => new ApiKeyStatusRepository(sp.GetRequiredService<AppDbContext>()));
        services.AddScoped<IApiKeyActionRepository>(sp => new ApiKeyActionRepository(sp.GetRequiredService<AppDbContext>()));

        return services;
    }
}
