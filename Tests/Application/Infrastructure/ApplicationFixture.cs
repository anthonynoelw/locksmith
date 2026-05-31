namespace Application.Infrastructure;

using Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Shared fixture that boots the full application once per test collection.
/// Provides a live <see cref="HttpClient"/> and the root <see cref="IServiceProvider"/>
/// for resolving services in test setup and teardown.
/// </summary>
/// <remarks>
/// When EF Core is added, extend <see cref="InitializeAsync"/> to create the schema
/// and seed baseline data:
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
    private WebApplicationFactory<Program>? _factory;

    /// <summary>Gets the HTTP client targeting the in-process application.</summary>
    public HttpClient Client { get; private set; } = default!;

    /// <summary>Gets the root service provider of the running application.</summary>
    public IServiceProvider Services => _factory!.Services;

    /// <inheritdoc/>
    public Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");

                builder.ConfigureServices(services =>
                {
                    // Register this assembly as an application part so ASP.NET Core
                    // discovers TestController alongside the production controllers.
                    services.AddMvc()
                        .AddApplicationPart(typeof(ApplicationFixture).Assembly);
                });
            });

        Client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DisposeAsync()
    {
        Client.Dispose();
        _factory?.Dispose();
        return Task.CompletedTask;
    }
}
