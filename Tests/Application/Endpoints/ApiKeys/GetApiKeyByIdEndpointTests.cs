namespace Application.Endpoints.ApiKeys;

using System.Net;
using System.Text.Json;
using Api.Responses;
using Application.Infrastructure;
using FluentAssertions;

/// <summary>
/// Integration tests for GET /api/v{version}/api-keys/{id} endpoint.
/// Requires PostgreSQL running on localhost:5432 with database 'locksmith_test'.
/// </summary>
[Collection("Application")]
public sealed class GetApiKeyByIdEndpointTests(ApplicationFixture fixture) : ApplicationTestBase(fixture)
{
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GET_GetApiKeyById_WithValidId_Returns200Ok()
    {
        // First, create a key
        var createRequest = new { actions = new[] { 0 } }; // Read
        StringContent createContent = new StringContent(
            JsonSerializer.Serialize(createRequest, _jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json");

        using var createMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-keys")
        {
            Content = createContent,
        };
        createMessage.Headers.Authorization = new ("Bearer", "test-bearer-token");

        HttpResponseMessage createResponse = await Client.SendAsync(createMessage);
        string createBody = await createResponse.Content.ReadAsStringAsync();
        CreateApiKeyResponse? createdKey = JsonSerializer.Deserialize<CreateApiKeyResponse>(createBody, _jsonOptions);

        createdKey.Should().NotBeNull();
        Guid keyId = createdKey!.Id;

        // Now get the key by ID
        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/api-keys/{keyId}");
        getRequest.Headers.Authorization = new ("Bearer", "test-bearer-token");

        HttpResponseMessage getResponse = await Client.SendAsync(getRequest);

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GET_GetApiKeyById_ReturnsMetadataResponse()
    {
        // Create a key
        var createRequest = new { actions = new[] { 1, 2 } }; // Write, Delete
        StringContent createContent = new StringContent(
            JsonSerializer.Serialize(createRequest, _jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json");

        using var createMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-keys")
        {
            Content = createContent,
        };
        createMessage.Headers.Authorization = new ("Bearer", "test-bearer-token");

        HttpResponseMessage createResponse = await Client.SendAsync(createMessage);
        string createBody = await createResponse.Content.ReadAsStringAsync();
        CreateApiKeyResponse? createdKey = JsonSerializer.Deserialize<CreateApiKeyResponse>(createBody, _jsonOptions);

        createdKey.Should().NotBeNull();
        Guid keyId = createdKey!.Id;

        // Get the key
        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/api-keys/{keyId}");
        getRequest.Headers.Authorization = new ("Bearer", "test-bearer-token");

        HttpResponseMessage getResponse = await Client.SendAsync(getRequest);
        string getBody = await getResponse.Content.ReadAsStringAsync();

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        getBody.Should().NotBeNullOrEmpty();

        ApiKeyMetadataResponse? metadata = JsonSerializer.Deserialize<ApiKeyMetadataResponse>(getBody, _jsonOptions);

        metadata.Should().NotBeNull();
        metadata!.Id.Should().Be(keyId);
        metadata.MaskedSecretHash.Should().StartWith("****");
        metadata.CreatedAt.Should().NotBe(default);
        metadata.ExpiresAt.Should().NotBe(default);
        metadata.Actions.Should().Contain(new[] { "Write", "Delete" });
    }

    [Fact]
    public async Task GET_GetApiKeyById_WithNonExistentId_Returns404NotFound()
    {
        Guid nonExistentId = Guid.NewGuid();

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/api-keys/{nonExistentId}");
        getRequest.Headers.Authorization = new ("Bearer", "test-bearer-token");

        HttpResponseMessage getResponse = await Client.SendAsync(getRequest);

        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
