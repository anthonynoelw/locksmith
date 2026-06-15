namespace Application.Infrastructure;

using Api;
using global::Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Testcontainers.PostgreSql;

/// <summary>
/// Shared fixture that boots the full application once per test collection.
/// Automatically spins up a PostgreSQL container via Testcontainers.
/// Provides a live <see cref="HttpClient"/> and the root <see cref="IServiceProvider"/>
/// for resolving services in test setup and teardown.
/// </summary>
/// <remarks>
/// Extend <see cref="InitializeAsync"/> to create the schema and seed baseline data:
/// <code>
/// using var scope = Services.CreateScope();
/// var db = scope.ServiceProvider.GetRequiredService&lt;AppDbContext&gt;();
/// await db.Database.EnsureCreatedAsync();
/// </code>
/// Each test class that mutates state should truncate affected tables in its own
/// <c>InitializeAsync</c> implementation to guarantee isolation between tests.
/// </remarks>
public sealed class ApplicationFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private WebApplicationFactory<Program>? _factory;

    /// <summary>Gets the HTTP client targeting the in-process application.</summary>
    public HttpClient Client { get; private set; } = default!;

    /// <summary>Gets the root service provider of the running application.</summary>
    public IServiceProvider Services => _factory!.Services;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithDatabase("locksmith_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await _container.StartAsync();

        string connectionString = _container.GetConnectionString();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");

                builder.ConfigureAppConfiguration((_, config) =>
                {
                    var testSettings = new Dictionary<string, string?>
                    {
                        { "Api:Name", "Test API" },
                        { "Api:BearerToken", "test-bearer-token" },
                        { "ConnectionStrings:DefaultConnection", connectionString },
                        { "ConnectionStrings:Redis", "localhost:6379,abortConnect=false" },
                        { "Cryptography:DegreeOfParallelism", "1" },
                        { "Cryptography:MemorySize", "65536" },
                        { "Cryptography:Iterations", "3" },
                    };
                    config.AddInMemoryCollection(testSettings);
                });

                builder.ConfigureServices(services =>
                {
                    // Register this assembly as an application part so ASP.NET Core
                    // discovers TestController alongside the production controllers.
                    services.AddMvc()
                        .AddApplicationPart(typeof(ApplicationFixture).Assembly);

                    // Replace DbContext with containerized connection.
                    var dbContextDescriptors = services.Where(d => d.ServiceType == typeof(AppDbContext)).ToList();
                    foreach (ServiceDescriptor descriptor in dbContextDescriptors)
                    {
                        services.Remove(descriptor);
                    }

                    services.AddDbContext<AppDbContext>(options =>
                        options.UseNpgsql(connectionString));

                    // Replace Redis with in-memory distributed cache for tests.
                    var cacheDescriptors = services.Where(d => d.ServiceType == typeof(IDistributedCache)).ToList();
                    foreach (ServiceDescriptor descriptor in cacheDescriptors)
                    {
                        services.Remove(descriptor);
                    }

                    services.AddDistributedMemoryCache();

                    // Use Redis with abortConnect=false so tests can run without a live Redis instance.
                    var connectionDescriptors = services.Where(d => d.ServiceType == typeof(IConnectionMultiplexer)).ToList();
                    foreach (ServiceDescriptor descriptor in connectionDescriptors)
                    {
                        services.Remove(descriptor);
                    }

                    services.AddSingleton<IConnectionMultiplexer>(
                        ConnectionMultiplexer.Connect("localhost:6379,abortConnect=false"));
                });
            });

        Client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        await MigrateDatabaseAsync();
    }

    private async Task MigrateDatabaseAsync()
    {
        using IServiceScope scope = Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        Client.Dispose();
        _factory?.Dispose();

        if (_container != null)
        {
            await _container.StopAsync();
        }
    }
}
