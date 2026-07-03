namespace Application.Endpoints.ApiKeys;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Api.Responses;
using Application.Infrastructure;
using Domain.Enums;
using FluentAssertions;

/// <summary>
/// Integration tests for the /api/v{version}/api-keys/status endpoints.
/// </summary>
[Collection("Application")]
public sealed class ApiKeyStatusEndpointTests(ApplicationFixture fixture) : ApplicationTestBase(fixture)
{
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GET_CurrentStatus_WithValidId_Returns200OkWithInactiveStatus()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();

        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/api-keys/status/{createdKey.Id}");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-bearer-token");

        HttpResponseMessage response = await Client.SendAsync(requestMessage);
        string body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ApiKeyStatusResponse? status = JsonSerializer.Deserialize<ApiKeyStatusResponse>(body, _jsonOptions);
        status.Should().NotBeNull();
        status!.Status.Should().Be("Inactive");
    }

    [Fact]
    public async Task GET_CurrentStatus_WithUnknownId_Returns404NotFound()
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/api-keys/status/{Guid.NewGuid()}");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-bearer-token");

        HttpResponseMessage response = await Client.SendAsync(requestMessage);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GET_CurrentStatus_WithoutBearerToken_Returns401Unauthorized()
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/api-keys/status/{Guid.NewGuid()}");

        HttpResponseMessage response = await Client.SendAsync(requestMessage);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_History_WithValidId_Returns200OkWithSingleInactiveEntry()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();

        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/api-keys/status/{createdKey.Id}/history");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-bearer-token");

        HttpResponseMessage response = await Client.SendAsync(requestMessage);
        string body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        List<ApiKeyStatusHistoryResponse>? history = JsonSerializer.Deserialize<List<ApiKeyStatusHistoryResponse>>(body, _jsonOptions);
        history.Should().NotBeNull();
        history!.Should().ContainSingle(s => s.Status == "Inactive");
    }

    [Fact]
    public async Task GET_History_WithUnknownId_Returns200OkWithEmptyList()
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/api-keys/status/{Guid.NewGuid()}/history");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-bearer-token");

        HttpResponseMessage response = await Client.SendAsync(requestMessage);
        string body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        List<ApiKeyStatusHistoryResponse>? history = JsonSerializer.Deserialize<List<ApiKeyStatusHistoryResponse>>(body, _jsonOptions);
        history.Should().BeEmpty();
    }

    [Fact]
    public async Task PATCH_Update_WithValidIdempotencyKeyAndStatus_Returns200OkAndUpdatesCurrentStatus()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();

        HttpResponseMessage updateResponse = await SendUpdateAsync(createdKey.IdempotencyKey, ApiKeyStatusEnum.Active);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var statusMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/api-keys/status/{createdKey.Id}");
        statusMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-bearer-token");
        HttpResponseMessage statusResponse = await Client.SendAsync(statusMessage);
        string statusBody = await statusResponse.Content.ReadAsStringAsync();
        ApiKeyStatusResponse? status = JsonSerializer.Deserialize<ApiKeyStatusResponse>(statusBody, _jsonOptions);

        status.Should().NotBeNull();
        status!.Status.Should().Be("Active");
    }

    [Fact]
    public async Task PATCH_Update_WithInvalidIdempotencyKey_Returns404NotFound()
    {
        HttpResponseMessage response = await SendUpdateAsync("invalid-idempotency-key", ApiKeyStatusEnum.Active);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PATCH_Update_WithMissingStatus_Returns422UnprocessableEntity()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();
        var updateRequest = new { idempotencyKey = createdKey.IdempotencyKey };
        using var content = new StringContent(JsonSerializer.Serialize(updateRequest, _jsonOptions), Encoding.UTF8, "application/json");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/api-keys/status/update")
        {
            Content = content,
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-bearer-token");

        HttpResponseMessage response = await Client.SendAsync(requestMessage);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task PATCH_Update_WhenCurrentStatusIsRevoked_Returns409Conflict()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();
        (await SendUpdateAsync(createdKey.IdempotencyKey, ApiKeyStatusEnum.Revoked)).StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage response = await SendUpdateAsync(createdKey.IdempotencyKey, ApiKeyStatusEnum.Active);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private async Task<CreateApiKeyResponse> CreateApiKeyAsync()
    {
        object request = new { actions = new[] { 0 } }; // Read
        using var content = new StringContent(JsonSerializer.Serialize(request, _jsonOptions), Encoding.UTF8, "application/json");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-keys")
        {
            Content = content,
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-bearer-token");

        HttpResponseMessage response = await Client.SendAsync(requestMessage);
        string body = await response.Content.ReadAsStringAsync();
        CreateApiKeyResponse? createdKey = JsonSerializer.Deserialize<CreateApiKeyResponse>(body, _jsonOptions);

        createdKey.Should().NotBeNull();
        return createdKey!;
    }

    private async Task<HttpResponseMessage> SendUpdateAsync(string idempotencyKey, ApiKeyStatusEnum status)
    {
        var updateRequest = new { idempotencyKey, status = (int)status };
        using var content = new StringContent(JsonSerializer.Serialize(updateRequest, _jsonOptions), Encoding.UTF8, "application/json");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/api-keys/status/update")
        {
            Content = content,
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-bearer-token");

        return await Client.SendAsync(requestMessage);
    }
}
