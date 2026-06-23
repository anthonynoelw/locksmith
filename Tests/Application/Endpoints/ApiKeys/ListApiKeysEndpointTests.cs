namespace Application.Endpoints.ApiKeys;

using System.Net;
using System.Text.Json;
using Api.Responses;
using Application.Infrastructure;
using FluentAssertions;

/// <summary>
/// Integration tests for GET /api/v{version}/api-keys endpoint.
/// Requires PostgreSQL running on localhost:5432 with database 'locksmith_test'.
/// </summary>
[Collection("Application")]
public sealed class ListApiKeysEndpointTests(ApplicationFixture fixture) : ApplicationTestBase(fixture)
{
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GET_ListApiKeys_WithValidRequest_Returns200Ok()
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/v1/api-keys");
        requestMessage.Headers.Authorization = new ("Bearer", "test-bearer-token");

        HttpResponseMessage response = await Client.SendAsync(requestMessage);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GET_ListApiKeys_ReturnsListResponseWithPagination()
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/v1/api-keys");
        requestMessage.Headers.Authorization = new ("Bearer", "test-bearer-token");

        HttpResponseMessage response = await Client.SendAsync(requestMessage);
        string jsonBody = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        jsonBody.Should().NotBeNullOrEmpty();

        ListApiKeysResponse? body = JsonSerializer.Deserialize<ListApiKeysResponse>(jsonBody, _jsonOptions);

        body.Should().NotBeNull();
        body!.Items.Should().NotBeNull();
        body.Total.Should().BeGreaterThanOrEqualTo(0);
        body.Limit.Should().Be(50); // default
        body.Offset.Should().Be(0); // default
    }

    [Fact]
    public async Task GET_ListApiKeys_WithCustomPagination_RespectsLimitAndOffset()
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/v1/api-keys?limit=10&offset=0");
        requestMessage.Headers.Authorization = new ("Bearer", "test-bearer-token");

        HttpResponseMessage response = await Client.SendAsync(requestMessage);
        string jsonBody = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        ListApiKeysResponse? body = JsonSerializer.Deserialize<ListApiKeysResponse>(jsonBody, _jsonOptions);

        body.Should().NotBeNull();
        body!.Limit.Should().Be(10);
        body.Offset.Should().Be(0);
    }

    [Fact]
    public async Task GET_ListApiKeys_WithInvalidLimit_UsesDefault()
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/v1/api-keys?limit=2000&offset=0");
        requestMessage.Headers.Authorization = new ("Bearer", "test-bearer-token");

        HttpResponseMessage response = await Client.SendAsync(requestMessage);
        string jsonBody = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        ListApiKeysResponse? body = JsonSerializer.Deserialize<ListApiKeysResponse>(jsonBody, _jsonOptions);

        body.Should().NotBeNull();
        body!.Limit.Should().Be(50); // default when > 1000
    }

    [Fact]
    public async Task GET_ListApiKeys_IncludesMetadataForCreatedKey()
    {
        var createRequest = new { actions = new[] { 0, 1 } }; // Read, Write
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
        Guid createdKeyId = createdKey!.Id;

        // Now list and verify the created key is in the list
        using var listMessage = new HttpRequestMessage(HttpMethod.Get, "/api/v1/api-keys");
        listMessage.Headers.Authorization = new ("Bearer", "test-bearer-token");

        HttpResponseMessage listResponse = await Client.SendAsync(listMessage);
        string listBody = await listResponse.Content.ReadAsStringAsync();
        ListApiKeysResponse? list = JsonSerializer.Deserialize<ListApiKeysResponse>(listBody, _jsonOptions);

        list.Should().NotBeNull();
        list!.Items.Should().Contain(k => k.Id == createdKeyId);
        ApiKeyMetadataResponse? createdKeyMetadata = list.Items.FirstOrDefault(k => k.Id == createdKeyId);
        createdKeyMetadata.Should().NotBeNull();
        createdKeyMetadata!.MaskedSecretHash.Should().StartWith("****");
        createdKeyMetadata.Actions.Should().Contain(new[] { "Read", "Write" });
    }
}
