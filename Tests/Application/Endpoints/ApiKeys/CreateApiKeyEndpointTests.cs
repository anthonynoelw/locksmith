namespace Application.Endpoints.ApiKeys;

using System.Net;
using System.Text.Json;
using Api.Responses;
using Infrastructure;
using Domain.Enums;
using FluentAssertions;

/// <summary>
/// Integration tests for POST /api/v{version}/api-keys endpoint.
/// Requires PostgreSQL running on localhost:5432 with database 'locksmith_test'.
/// </summary>
[Collection("Application")]
public sealed class CreateApiKeyEndpointTests(ApplicationFixture fixture) : ApplicationTestBase(fixture)
{
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    [Fact]
    public async Task POST_CreateApiKey_WithValidRequest_Returns201Created()
    {
        object request = new { actions = new[] { 0, 1 } }; // Read=0, Write=1
        StringContent content = new StringContent(JsonSerializer.Serialize(request, _jsonOptions), System.Text.Encoding.UTF8, "application/json");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-key")
        {
            Content = content,
        };
        requestMessage.Headers.Authorization = new ("Bearer", "test-bearer-token");

        HttpResponseMessage response = await Client.SendAsync(requestMessage);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task POST_CreateApiKey_ReturnsIdSecretAndIdempotencyKey()
    {
        object request = new { actions = new[] { 0 } }; // Read only
        StringContent content = new StringContent(JsonSerializer.Serialize(request, _jsonOptions), System.Text.Encoding.UTF8, "application/json");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-key")
        {
            Content = content,
        };
        requestMessage.Headers.Authorization = new ("Bearer", "test-bearer-token");

        HttpResponseMessage response = await Client.SendAsync(requestMessage);
        string jsonBody = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        jsonBody.Should().NotBeNullOrEmpty();

        CreateApiKeyResponse? body = JsonSerializer.Deserialize<CreateApiKeyResponse>(jsonBody, _jsonOptions);

        body.Should().NotBeNull();
        body!.Id.Should().NotBeEmpty();
        body.Secret.Should().StartWith("lk_");
        body.IdempotencyKey.Should().HaveLength(128);
    }
}
