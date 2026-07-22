namespace Application.Endpoints.ApiKeys;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Api.Responses;
using Application.Infrastructure;
using FluentAssertions;

/// <summary>
/// Integration tests for DELETE /api/v{version}/api-key. The key is identified by its idempotency
/// key in the body. These also guard the invariant that deletion is permanent: neither the deleted
/// key's secret nor its idempotency key may be used for anything afterward.
/// </summary>
[Collection("Application")]
public sealed class DeleteApiKeyEndpointTests(ApplicationFixture fixture) : ApplicationTestBase(fixture)
{
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    [Fact]
    public async Task DELETE_WithValidIdempotencyKey_Returns204NoContent()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();

        HttpResponseMessage response = await SendDeleteAsync(createdKey.IdempotencyKey);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DELETE_WithUnknownIdempotencyKey_Returns404NotFound()
    {
        HttpResponseMessage response = await SendDeleteAsync(Guid.NewGuid().ToString());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DELETE_WithoutBearerToken_Returns401Unauthorized()
    {
        using var content = new StringContent(
            JsonSerializer.Serialize(new { idempotencyKey = Guid.NewGuid().ToString() }, _jsonOptions),
            Encoding.UTF8,
            "application/json");
        using var requestMessage = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/api-key") { Content = content };

        HttpResponseMessage response = await Client.SendAsync(requestMessage);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DELETE_CalledTwice_SecondCallReturns404NotFound()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();
        (await SendDeleteAsync(createdKey.IdempotencyKey)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage secondResponse = await SendDeleteAsync(createdKey.IdempotencyKey);

        secondResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DELETE_ThenGetCurrent_WithOldSecret_Returns404NotFound()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();
        (await SendDeleteAsync(createdKey.IdempotencyKey)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage readResponse = await GetWithApiKeyAsync("/api/v1/api-key", createdKey.Secret);

        readResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DELETE_ThenGrant_WithOldIdempotencyKey_Returns404NotFound()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();
        (await SendDeleteAsync(createdKey.IdempotencyKey)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var content = new StringContent(
            JsonSerializer.Serialize(new { idempotencyKey = createdKey.IdempotencyKey }, _jsonOptions),
            Encoding.UTF8,
            "application/json");
        using var grantRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-key/actions/Write")
        {
            Content = content,
        };
        grantRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", BEARER_TOKEN);

        HttpResponseMessage grantResponse = await Client.SendAsync(grantRequest);

        grantResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DELETE_ThenRotate_WithOldIdempotencyKey_Returns404NotFound()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();
        (await SendDeleteAsync(createdKey.IdempotencyKey)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var content = new StringContent(
            JsonSerializer.Serialize(new { idempotencyKey = createdKey.IdempotencyKey }, _jsonOptions),
            Encoding.UTF8,
            "application/json");
        using var rotateRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-key/rotate")
        {
            Content = content,
        };
        rotateRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", BEARER_TOKEN);

        HttpResponseMessage rotateResponse = await Client.SendAsync(rotateRequest);

        rotateResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<HttpResponseMessage> SendDeleteAsync(string idempotencyKey)
    {
        using var content = new StringContent(
            JsonSerializer.Serialize(new { idempotencyKey }, _jsonOptions),
            Encoding.UTF8,
            "application/json");
        using var requestMessage = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/api-key") { Content = content };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", BEARER_TOKEN);

        return await Client.SendAsync(requestMessage);
    }
}
