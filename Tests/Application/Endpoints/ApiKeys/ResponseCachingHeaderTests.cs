namespace Application.Endpoints.ApiKeys;

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Api.Responses;
using Api.Settings;
using Application.Infrastructure;
using Domain;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Verifies response cache-control directives: no-store for endpoints that mutate state or return
/// secret material, and a short private cache scoped to the caller's own API key (via
/// <c>Vary: X-Api-Key</c>) for read-only endpoints marked <c>[Cacheable]</c>.
/// </summary>
[Collection("Application")]
public sealed class ResponseCachingHeaderTests(ApplicationFixture fixture) : ApplicationTestBase(fixture)
{
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    [Fact]
    public async Task POST_CreateApiKey_Response_IsNoStore()
    {
        object request = new { actions = new[] { 0 } };
        using var content = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json");
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-key") { Content = content };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", BEARER_TOKEN);

        HttpResponseMessage response = await Client.SendAsync(requestMessage);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.CacheControl.Should().NotBeNull();
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task POST_RetrieveSecret_Response_IsNoStore()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();

        using var content = new StringContent(
            JsonSerializer.Serialize(new { idempotencyKey = createdKey.IdempotencyKey }),
            System.Text.Encoding.UTF8,
            "application/json");
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-key/secret")
        {
            Content = content,
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", BEARER_TOKEN);

        HttpResponseMessage response = await Client.SendAsync(requestMessage);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl.Should().NotBeNull();
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Theory]
    [InlineData("/api/v1/api-key")]
    [InlineData("/api/v1/api-key/actions")]
    [InlineData("/api/v1/api-key/status")]
    [InlineData("/api/v1/api-key/status/history")]
    public async Task GET_CacheableEndpoint_Response_IsPrivateCacheableAndVariesByApiKey(string url)
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();
        int expectedMaxAge = Services.GetRequiredService<IOptions<CacheSettings>>().Value.ApiKeyReadSeconds;

        HttpResponseMessage response = await GetWithApiKeyAsync(url, createdKey.Secret);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl.Should().NotBeNull();
        response.Headers.CacheControl!.NoStore.Should().BeFalse();
        response.Headers.CacheControl.Private.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromSeconds(expectedMaxAge));
        response.Headers.Vary.Should().Contain(WellKnown.RequestHeaders.API_KEY);
    }

    [Fact]
    public async Task GET_ListAll_Response_IsNoStore()
    {
        // /all is the bulk admin listing, not scoped to a single caller's key, so it stays no-store.
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/v1/api-key/all");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", BEARER_TOKEN);

        HttpResponseMessage response = await Client.SendAsync(requestMessage);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl.Should().NotBeNull();
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }
}
