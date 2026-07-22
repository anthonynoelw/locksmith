namespace Application.Endpoints.ApiKeys;

using System.Net;
using System.Text.Json;
using Api.Responses;
using Application.Infrastructure;
using FluentAssertions;

/// <summary>
/// Integration tests for POST /api/v{version}/api-keys/validate endpoint.
/// Requires PostgreSQL running on localhost:5432 with database 'locksmith_test'.
/// </summary>
[Collection("Application")]
public sealed class ValidateApiKeySecretEndpointTests(ApplicationFixture fixture) : ApplicationTestBase(fixture)
{
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    [Fact]
    public async Task POST_ValidateApiKeySecret_WithValidSecret_Returns200Ok()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();
        string plainSecret = createdKey.Secret;

        // Validate the secret
        var validateRequest = new { secret = plainSecret };
        StringContent validateContent = new StringContent(
            JsonSerializer.Serialize(validateRequest, _jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json");

        using var validateMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-key/validate")
        {
            Content = validateContent,
        };
        validateMessage.Headers.Authorization = new ("Bearer", "test-bearer-token");

        HttpResponseMessage validateResponse = await Client.SendAsync(validateMessage);

        validateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task POST_ValidateApiKeySecret_WhenKeyIsInactive_ReturnsIsValidFalse()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();
        string plainSecret = createdKey.Secret;
        Guid keyId = createdKey.Id;

        // Validate the secret
        var validateRequest = new { secret = plainSecret };
        StringContent validateContent = new StringContent(
            JsonSerializer.Serialize(validateRequest, _jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json");

        using var validateMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-key/validate")
        {
            Content = validateContent,
        };
        validateMessage.Headers.Authorization = new ("Bearer", "test-bearer-token");

        HttpResponseMessage validateResponse = await Client.SendAsync(validateMessage);
        string validateBody = await validateResponse.Content.ReadAsStringAsync();

        validateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        ValidateApiKeySecretResponse? validationResult = JsonSerializer.Deserialize<ValidateApiKeySecretResponse>(
            validateBody,
            _jsonOptions);

        validationResult.Should().NotBeNull();
        validationResult!.ApiKeyId.Should().Be(keyId);
        validationResult.IsValid.Should().BeFalse(); // Initially inactive
    }

    [Fact]
    public async Task POST_ValidateApiKeySecret_WithInvalidSecret_Returns404NotFound()
    {
        var validateRequest = new { secret = "invalid-secret" };
        StringContent validateContent = new StringContent(
            JsonSerializer.Serialize(validateRequest, _jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json");

        using var validateMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-key/validate")
        {
            Content = validateContent,
        };
        validateMessage.Headers.Authorization = new ("Bearer", "test-bearer-token");

        HttpResponseMessage validateResponse = await Client.SendAsync(validateMessage);

        validateResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task POST_ValidateApiKeySecret_WithEmptySecret_Returns422UnprocessableEntity()
    {
        var validateRequest = new { secret = string.Empty };
        StringContent validateContent = new StringContent(
            JsonSerializer.Serialize(validateRequest, _jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json");

        using var validateMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-key/validate")
        {
            Content = validateContent,
        };
        validateMessage.Headers.Authorization = new ("Bearer", "test-bearer-token");

        HttpResponseMessage validateResponse = await Client.SendAsync(validateMessage);

        validateResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
