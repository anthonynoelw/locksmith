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
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();
        Guid keyId = createdKey.Id;

        // Now get the key by ID
        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/api-keys/{keyId}");
        getRequest.Headers.Authorization = new ("Bearer", "test-bearer-token");

        HttpResponseMessage getResponse = await Client.SendAsync(getRequest);

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GET_GetApiKeyById_ReturnsMetadataResponse()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync([1, 2]); // Write, Delete
        Guid keyId = createdKey.Id;

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
