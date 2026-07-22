namespace Application.Endpoints.ApiKeys;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using global::Api;
using global::Application.Infrastructure;
using global::Application.Interfaces.Services;
using Domain;
using Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// HTTP-level tests for the rate limiter. Uses a self-contained factory with a deterministic
/// <see cref="FakeRateLimiter"/> and <see cref="FakeGetApiKeyBySecretService"/> so the reject path can
/// be exercised without a live Redis or database — the filters short-circuit before any handler or
/// real data access runs.
/// </summary>
public sealed class RateLimitEndpointTests : IDisposable
{
    private const string BEARER_TOKEN = "test-bearer-token";
    private const string API_KEY_SECRET = "lk_test-secret";

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
                    var descriptors = services
                        .Where(d => d.ServiceType == typeof(IRateLimiter) || d.ServiceType == typeof(IGetApiKeyBySecretService))
                        .ToList();
                    foreach (ServiceDescriptor descriptor in descriptors)
                    {
                        services.Remove(descriptor);
                    }

                    services.AddSingleton<IRateLimiter>(new FakeRateLimiter(rejected));
                    services.AddSingleton<IGetApiKeyBySecretService>(
                        new FakeGetApiKeyBySecretService(API_KEY_SECRET, Guid.NewGuid()));
                });
            });
    }

    /// <summary>Gets the method/URL/body of every idempotencyKey- or secret-identified mutation endpoint.</summary>
    public static TheoryData<HttpMethod, string, object> CredentialRateLimitedEndpoints => new ()
    {
        { HttpMethod.Post, "/api/v1/api-key/secret", new { idempotencyKey = "idem-key" } },
        { HttpMethod.Post, "/api/v1/api-key/validate", new { secret = "lk_some-secret" } },
        { HttpMethod.Post, "/api/v1/api-key/rotate", new { idempotencyKey = "idem-key" } },
        { HttpMethod.Delete, "/api/v1/api-key", new { idempotencyKey = "idem-key" } },
        { HttpMethod.Put, "/api/v1/api-key/actions", new { idempotencyKey = "idem-key", actions = Array.Empty<string>() } },
        { HttpMethod.Post, "/api/v1/api-key/actions/Read", new { idempotencyKey = "idem-key" } },
        { HttpMethod.Delete, "/api/v1/api-key/actions/Read", new { idempotencyKey = "idem-key" } },
        { HttpMethod.Patch, "/api/v1/api-key/status", new { idempotencyKey = "idem-key", status = (int)ApiKeyStatusEnum.Active } },
    };

    [Theory]
    [InlineData("/api/v1/api-key")]
    [InlineData("/api/v1/api-key/actions")]
    [InlineData("/api/v1/api-key/status")]
    [InlineData("/api/v1/api-key/status/history")]
    public async Task RateLimitedEndpoint_WhenQuotaExceeded_Returns429WithQuotaHeaders(string url)
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", BEARER_TOKEN);
        request.Headers.Add("X-Api-Key", API_KEY_SECRET);

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter!.Delta.Should().Be(TimeSpan.FromSeconds(30));
        response.Headers.GetValues(WellKnown.RateLimitHeaders.LIMIT).Should().ContainSingle().Which.Should().Be("100");
        response.Headers.GetValues(WellKnown.RateLimitHeaders.REMAINING).Should().ContainSingle().Which.Should().Be("0");
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Theory]
    [MemberData(nameof(CredentialRateLimitedEndpoints))]
    public async Task CredentialRateLimitedEndpoint_WhenQuotaExceeded_Returns429WithQuotaHeaders(
        HttpMethod method, string url, object body)
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        using var request = new HttpRequestMessage(method, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", BEARER_TOKEN);

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter!.Delta.Should().Be(TimeSpan.FromSeconds(30));
        response.Headers.GetValues(WellKnown.RateLimitHeaders.LIMIT).Should().ContainSingle().Which.Should().Be("100");
        response.Headers.GetValues(WellKnown.RateLimitHeaders.REMAINING).Should().ContainSingle().Which.Should().Be("0");
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task UnauthenticatedRequest_IsNotRateLimited_Returns401()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        HttpResponseMessage response = await client.GetAsync("/api/v1/api-key");

        // Authorization runs before the resolve/rate-limit filters, so a missing token yields 401, not 429.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _factory.Dispose();
    }
}
