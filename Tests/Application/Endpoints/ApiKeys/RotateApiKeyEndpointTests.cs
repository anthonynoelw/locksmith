namespace Application.Endpoints.ApiKeys;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Api.Responses;
using Application.Infrastructure;
using FluentAssertions;

/// <summary>
/// Integration tests for POST /api/v{version}/api-key/rotate. The key is identified by its
/// idempotency key in the body. These also guard the invariant that rotation retires the old key:
/// neither its old secret nor its old idempotency key may be used for anything afterward.
/// </summary>
[Collection("Application")]
public sealed class RotateApiKeyEndpointTests(ApplicationFixture fixture) : ApplicationTestBase(fixture)
{
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    [Fact]
    public async Task POST_Rotate_WithValidIdempotencyKey_Returns201CreatedWithNewSecret()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();

        HttpResponseMessage response = await SendRotateAsync(createdKey.IdempotencyKey);
        string body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        CreateApiKeyResponse? rotated = JsonSerializer.Deserialize<CreateApiKeyResponse>(body, _jsonOptions);

        rotated.Should().NotBeNull();
        rotated!.Id.Should().NotBe(createdKey.Id);
        rotated.Secret.Should().NotBe(createdKey.Secret);
        rotated.IdempotencyKey.Should().NotBe(createdKey.IdempotencyKey);
    }

    [Fact]
    public async Task POST_Rotate_WithUnknownIdempotencyKey_Returns404NotFound()
    {
        HttpResponseMessage response = await SendRotateAsync(Guid.NewGuid().ToString());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task POST_Rotate_WithoutBearerToken_Returns401Unauthorized()
    {
        using var content = new StringContent(
            JsonSerializer.Serialize(new { idempotencyKey = Guid.NewGuid().ToString() }, _jsonOptions),
            Encoding.UTF8,
            "application/json");
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-key/rotate") { Content = content };

        HttpResponseMessage response = await Client.SendAsync(requestMessage);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_Rotate_PreservesGrantedActions()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync(new[] { 0, 1 }); // Read, Write

        HttpResponseMessage response = await SendRotateAsync(createdKey.IdempotencyKey);
        string body = await response.Content.ReadAsStringAsync();
        CreateApiKeyResponse? rotated = JsonSerializer.Deserialize<CreateApiKeyResponse>(body, _jsonOptions);

        HttpResponseMessage metadataResponse = await GetWithApiKeyAsync("/api/v1/api-key", rotated!.Secret);
        string metadataBody = await metadataResponse.Content.ReadAsStringAsync();
        ApiKeyMetadataResponse? metadata = JsonSerializer.Deserialize<ApiKeyMetadataResponse>(metadataBody, _jsonOptions);

        metadata.Should().NotBeNull();
        metadata!.Actions.Should().BeEquivalentTo(new[] { "Read", "Write" });
    }

    [Fact]
    public async Task POST_Rotate_NewSecret_ResolvesViaGetCurrent_Returns200Ok()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();

        HttpResponseMessage response = await SendRotateAsync(createdKey.IdempotencyKey);
        string body = await response.Content.ReadAsStringAsync();
        CreateApiKeyResponse? rotated = JsonSerializer.Deserialize<CreateApiKeyResponse>(body, _jsonOptions);

        HttpResponseMessage getResponse = await GetWithApiKeyAsync("/api/v1/api-key", rotated!.Secret);

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task POST_Rotate_ThenGetCurrent_WithOldSecret_Returns404NotFound()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();
        (await SendRotateAsync(createdKey.IdempotencyKey)).StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage readResponse = await GetWithApiKeyAsync("/api/v1/api-key", createdKey.Secret);

        readResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task POST_Rotate_ThenRotate_WithOldIdempotencyKey_Returns404NotFound()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();
        (await SendRotateAsync(createdKey.IdempotencyKey)).StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage secondRotateResponse = await SendRotateAsync(createdKey.IdempotencyKey);

        secondRotateResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<HttpResponseMessage> SendRotateAsync(string idempotencyKey)
    {
        using var content = new StringContent(
            JsonSerializer.Serialize(new { idempotencyKey }, _jsonOptions),
            Encoding.UTF8,
            "application/json");
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-key/rotate") { Content = content };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", BEARER_TOKEN);

        return await Client.SendAsync(requestMessage);
    }
}
