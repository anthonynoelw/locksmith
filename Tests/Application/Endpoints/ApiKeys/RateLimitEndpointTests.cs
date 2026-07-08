namespace Application.Endpoints.ApiKeys;

using System.Net;
using System.Net.Http.Headers;
using global::Api;
using global::Application.Infrastructure;
using global::Application.Interfaces.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// HTTP-level tests for the rate limiter. Uses a self-contained factory with a deterministic
/// <see cref="FakeRateLimiter"/> so the reject path can be exercised without a live Redis or database —
/// the filter short-circuits before any handler or data access runs.
/// </summary>
public sealed class RateLimitEndpointTests : IDisposable
{
    private const string BEARER_TOKEN = "test-bearer-token";

    private readonly WebApplicationFactory<Program> _factory;

    /// <summary>Initializes a new instance of the <see cref="RateLimitEndpointTests"/> class.</summary>
    public RateLimitEndpointTests()
    {
        var rejected = new RateLimitResult(
            IsAllowed: false,
            Limit: 100,
            Remaining: 0,
            ResetAt: DateTimeOffset.UtcNow.AddSeconds(30),
            RetryAfter: TimeSpan.FromSeconds(30));

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");

                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        { "Api:Name", "Test API" },
                        { "Api:BearerToken", BEARER_TOKEN },
                        { "ConnectionStrings:DefaultConnection", "Host=localhost;Database=none;Username=none;Password=none" },
                        { "ConnectionStrings:Redis", "localhost:6379,abortConnect=false" },
                        { "Cryptography:DegreeOfParallelism", "1" },
                        { "Cryptography:MemorySize", "65536" },
                        { "Cryptography:Iterations", "8" },
                    });
                });

                builder.ConfigureServices(services =>
                {
                    var descriptors = services.Where(d => d.ServiceType == typeof(IRateLimiter)).ToList();
                    foreach (ServiceDescriptor descriptor in descriptors)
                    {
                        services.Remove(descriptor);
                    }

                    services.AddSingleton<IRateLimiter>(new FakeRateLimiter(rejected));
                });
            });
    }

    [Fact]
    public async Task RateLimitedEndpoint_WhenQuotaExceeded_Returns429WithQuotaHeaders()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/api-keys/{Guid.NewGuid()}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", BEARER_TOKEN);

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter!.Delta.Should().Be(TimeSpan.FromSeconds(30));
        response.Headers.GetValues("X-RateLimit-Limit").Should().ContainSingle().Which.Should().Be("100");
        response.Headers.GetValues("X-RateLimit-Remaining").Should().ContainSingle().Which.Should().Be("0");
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task UnauthenticatedRequest_IsNotRateLimited_Returns401()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        HttpResponseMessage response = await client.GetAsync($"/api/v1/api-keys/{Guid.NewGuid()}");

        // Authorization runs before the rate-limit filter, so a missing token yields 401, not 429.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _factory.Dispose();
    }
}
