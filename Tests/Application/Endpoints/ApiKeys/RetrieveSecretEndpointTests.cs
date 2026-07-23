namespace Application.Endpoints.ApiKeys;

using System.Net;
using System.Text.Json;
using Api.Responses;
using Application.Infrastructure;
using FluentAssertions;

/// <summary>
/// Integration tests for POST /api/v{version}/api-keys/retrieve-secret endpoint.
/// Requires PostgreSQL running on localhost:5432 with database 'locksmith_test'.
/// </summary>
[Collection("Application")]
public sealed class RetrieveSecretEndpointTests(ApplicationFixture fixture) : ApplicationTestBase(fixture)
{
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    [Fact]
    public async Task POST_RetrieveSecret_WithValidIdempotencyKey_Returns200Ok()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();
        string idempotencyKey = createdKey.IdempotencyKey;

        // Retrieve the secret
        var retrieveRequest = new { idempotencyKey };
        StringContent retrieveContent = new StringContent(
            JsonSerializer.Serialize(retrieveRequest, _jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json");

        using var retrieveMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-key/secret")
        {
            Content = retrieveContent,
        };
        retrieveMessage.Headers.Authorization = new ("Bearer", "test-bearer-token");

        HttpResponseMessage retrieveResponse = await Client.SendAsync(retrieveMessage);

        retrieveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task POST_RetrieveSecret_ReturnsDecryptedSecret()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync([0, 1]); // Read, Write
        string originalSecret = createdKey.Secret;
        string idempotencyKey = createdKey.IdempotencyKey;
        Guid keyId = createdKey.Id;

        // Retrieve the secret
        var retrieveRequest = new { idempotencyKey };
        StringContent retrieveContent = new StringContent(
            JsonSerializer.Serialize(retrieveRequest, _jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json");

        using var retrieveMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-key/secret")
        {
            Content = retrieveContent,
        };
        retrieveMessage.Headers.Authorization = new ("Bearer", "test-bearer-token");

        HttpResponseMessage retrieveResponse = await Client.SendAsync(retrieveMessage);
        string retrieveBody = await retrieveResponse.Content.ReadAsStringAsync();

        retrieveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        RetrieveSecretResponse? result = JsonSerializer.Deserialize<RetrieveSecretResponse>(retrieveBody, _jsonOptions);

        result.Should().NotBeNull();
        result!.ApiKeyId.Should().Be(keyId);
        result.Secret.Should().Be(originalSecret); // Decrypted secret should match original
    }

    [Fact]
    public async Task POST_RetrieveSecret_WithInvalidIdempotencyKey_Returns404NotFound()
    {
        var retrieveRequest = new { idempotencyKey = "invalid-key" };
        StringContent retrieveContent = new StringContent(
            JsonSerializer.Serialize(retrieveRequest, _jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json");

        using var retrieveMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-key/secret")
        {
            Content = retrieveContent,
        };
        retrieveMessage.Headers.Authorization = new ("Bearer", "test-bearer-token");

        HttpResponseMessage retrieveResponse = await Client.SendAsync(retrieveMessage);

        retrieveResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task POST_RetrieveSecret_WithEmptyIdempotencyKey_Returns422UnprocessableEntity()
    {
        var retrieveRequest = new { idempotencyKey = string.Empty };
        StringContent retrieveContent = new StringContent(
            JsonSerializer.Serialize(retrieveRequest, _jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json");

        using var retrieveMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-key/secret")
        {
            Content = retrieveContent,
        };
        retrieveMessage.Headers.Authorization = new ("Bearer", "test-bearer-token");

        HttpResponseMessage retrieveResponse = await Client.SendAsync(retrieveMessage);

        retrieveResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
