namespace Application.Endpoints.ApiKeys;

using System.Net;
using System.Text.Json;
using Application.Infrastructure;
using FluentAssertions;

/// <summary>
/// Authorization tests for POST /api/v{version}/api-keys endpoint.
/// Verifies bearer token authentication is enforced on the endpoint.
/// </summary>
[Collection("Application")]
public sealed class CreateApiKeyAuthorizationTests(ApplicationFixture fixture) : ApplicationTestBase(fixture)
{
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    [Fact]
    public async Task POST_CreateApiKey_WithoutAuthorizationHeader_Returns401Unauthorized()
    {
        object request = new { actions = new[] { 0 } }; // Read only
        StringContent content = new StringContent(JsonSerializer.Serialize(request, _jsonOptions), System.Text.Encoding.UTF8, "application/json");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-keys")
        {
            Content = content,
        };

        HttpResponseMessage response = await Client.SendAsync(requestMessage);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_CreateApiKey_WithInvalidBearerToken_Returns401Unauthorized()
    {
        object request = new { actions = new[] { 0 } }; // Read only
        StringContent content = new StringContent(JsonSerializer.Serialize(request, _jsonOptions), System.Text.Encoding.UTF8, "application/json");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-keys")
        {
            Content = content,
        };
        requestMessage.Headers.Authorization = new ("Bearer", "invalid-token");

        HttpResponseMessage response = await Client.SendAsync(requestMessage);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_CreateApiKey_Unauthorized_ReturnsWWWAuthenticateHeader()
    {
        object request = new { actions = new[] { 0 } }; // Read only
        StringContent content = new StringContent(JsonSerializer.Serialize(request, _jsonOptions), System.Text.Encoding.UTF8, "application/json");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-keys")
        {
            Content = content,
        };

        HttpResponseMessage response = await Client.SendAsync(requestMessage);

        response.Headers.Should().Contain(x => x.Key == "WWW-Authenticate");
        response.Headers.FirstOrDefault(x => x.Key == "WWW-Authenticate").Value.Should()
            .ContainSingle("Bearer");
    }
}
