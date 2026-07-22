namespace Application.Endpoints.ApiKeys;

using System.Net;
using System.Text.Json;
using Application.Infrastructure;
using FluentAssertions;

/// <summary>
/// Authorization tests for retrieval endpoints (List, GetById, Validate, RetrieveSecret).
/// Requires PostgreSQL running on localhost:5432 with database 'locksmith_test'.
/// </summary>
[Collection("Application")]
public sealed class RetrievalEndpointAuthorizationTests(ApplicationFixture fixture) : ApplicationTestBase(fixture)
{
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GET_ListApiKeys_WithoutAuthorizationHeader_Returns401Unauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/api-key/all");

        HttpResponseMessage response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_ListApiKeys_WithInvalidBearerToken_Returns401Unauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/api-key/all");
        request.Headers.Authorization = new ("Bearer", "invalid-token");

        HttpResponseMessage response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_ListApiKeys_Unauthorized_ReturnsWWWAuthenticateHeader()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/api-key/all");

        HttpResponseMessage response = await Client.SendAsync(request);

        response.Headers.WwwAuthenticate.Should().NotBeEmpty();
        response.Headers.WwwAuthenticate.First().Scheme.Should().Be("Bearer");
    }

    [Fact]
    public async Task GET_GetApiKeyById_WithoutAuthorizationHeader_Returns401Unauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/api-key");

        HttpResponseMessage response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_GetApiKeyById_WithInvalidBearerToken_Returns401Unauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/api-key");
        request.Headers.Authorization = new ("Bearer", "invalid-token");

        HttpResponseMessage response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_ValidateApiKeySecret_WithoutAuthorizationHeader_Returns401Unauthorized()
    {
        var validateRequest = new { secret = "test-secret" };
        StringContent content = new StringContent(
            JsonSerializer.Serialize(validateRequest, _jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-key/validate")
        {
            Content = content,
        };

        HttpResponseMessage response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_ValidateApiKeySecret_WithInvalidBearerToken_Returns401Unauthorized()
    {
        var validateRequest = new { secret = "test-secret" };
        StringContent content = new StringContent(
            JsonSerializer.Serialize(validateRequest, _jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-key/validate")
        {
            Content = content,
            Headers =
            {
                { "Authorization", "Bearer invalid-token" },
            },
        };

        HttpResponseMessage response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_RetrieveSecret_WithoutAuthorizationHeader_Returns401Unauthorized()
    {
        var retrieveRequest = new { idempotencyKey = "test-key" };
        StringContent content = new StringContent(
            JsonSerializer.Serialize(retrieveRequest, _jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-key/secret")
        {
            Content = content,
        };

        HttpResponseMessage response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_RetrieveSecret_WithInvalidBearerToken_Returns401Unauthorized()
    {
        var retrieveRequest = new { idempotencyKey = "test-key" };
        StringContent content = new StringContent(
            JsonSerializer.Serialize(retrieveRequest, _jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-key/secret")
        {
            Content = content,
            Headers =
            {
                { "Authorization", "Bearer invalid-token" },
            },
        };

        HttpResponseMessage response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
