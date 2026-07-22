namespace Application.Endpoints.ApiKeys;

using System.Net;
using System.Text.Json;
using Api.Responses;
using Application.Infrastructure;
using FluentAssertions;

/// <summary>
/// Integration tests for GET /api/v{version}/api-key, which returns the metadata of the API key
/// identified by the X-Api-Key header.
/// Requires PostgreSQL running on localhost:5432 with database 'locksmith_test'.
/// </summary>
[Collection("Application")]
public sealed class GetApiKeyByIdEndpointTests(ApplicationFixture fixture) : ApplicationTestBase(fixture)
{
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GET_Current_WithValidSecret_Returns200Ok()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();

        HttpResponseMessage getResponse = await GetWithApiKeyAsync("/api/v1/api-key", createdKey.Secret);

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GET_Current_ReturnsMetadataResponse()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync([1, 2]); // Write, Delete

        HttpResponseMessage getResponse = await GetWithApiKeyAsync("/api/v1/api-key", createdKey.Secret);
        string getBody = await getResponse.Content.ReadAsStringAsync();

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        getBody.Should().NotBeNullOrEmpty();

        ApiKeyMetadataResponse? metadata = JsonSerializer.Deserialize<ApiKeyMetadataResponse>(getBody, _jsonOptions);

        metadata.Should().NotBeNull();
        metadata!.Id.Should().Be(createdKey.Id);
        metadata.MaskedSecretHash.Should().StartWith("****");
        metadata.CreatedAt.Should().NotBe(default);
        metadata.ExpiresAt.Should().NotBe(default);
        metadata.Actions.Should().Contain(new[] { "Write", "Delete" });
    }

    [Fact]
    public async Task GET_Current_WithUnknownSecret_Returns404NotFound()
    {
        HttpResponseMessage getResponse = await GetWithApiKeyAsync("/api/v1/api-key", "lk_nonexistent-secret");

        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GET_Current_WithUnknownSecret_DoesNotReflectSecretInResponseBody()
    {
        const string SECRET = "lk_super-secret-should-not-be-echoed";

        HttpResponseMessage getResponse = await GetWithApiKeyAsync("/api/v1/api-key", SECRET);
        string body = await getResponse.Content.ReadAsStringAsync();

        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        body.Should().NotContain(SECRET);
    }

    [Fact]
    public async Task GET_Current_WithUnknownSecret_ErrorResponse_IsNoStore()
    {
        // The 404 is produced by GlobalExceptionHandler in middleware, outside the MVC result pipeline,
        // so this guards that no-store is applied to error responses too.
        HttpResponseMessage getResponse = await GetWithApiKeyAsync("/api/v1/api-key", "lk_nonexistent-secret");

        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        getResponse.Headers.CacheControl.Should().NotBeNull();
        getResponse.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task GET_Current_WithoutApiKeyHeader_Returns400BadRequest()
    {
        using var getRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/api-key");
        getRequest.Headers.Authorization = new ("Bearer", BEARER_TOKEN);

        HttpResponseMessage getResponse = await Client.SendAsync(getRequest);

        getResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
