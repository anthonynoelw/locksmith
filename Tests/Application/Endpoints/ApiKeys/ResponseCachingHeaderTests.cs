namespace Application.Endpoints.ApiKeys;

using System.Net;
using Api.Responses;
using Application.Infrastructure;
using FluentAssertions;

/// <summary>
/// Verifies that responses carry no-store cache directives, since the API deals only in API key
/// material and its metadata.
/// </summary>
[Collection("Application")]
public sealed class ResponseCachingHeaderTests(ApplicationFixture fixture) : ApplicationTestBase(fixture)
{
    [Fact]
    public async Task POST_CreateApiKey_Response_IsNoStore()
    {
        // CreateApiKeyAsync issues POST /api/v1/api-key and returns after a successful create.
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();

        HttpResponseMessage readResponse = await GetWithApiKeyAsync("/api/v1/api-key", createdKey.Secret);

        readResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        readResponse.Headers.CacheControl.Should().NotBeNull();
        readResponse.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task POST_RetrieveSecret_Response_IsNoStore()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();

        using var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new { idempotencyKey = createdKey.IdempotencyKey }),
            System.Text.Encoding.UTF8,
            "application/json");
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-key/secret")
        {
            Content = content,
        };
        requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", BEARER_TOKEN);

        HttpResponseMessage response = await Client.SendAsync(requestMessage);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl.Should().NotBeNull();
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }
}
