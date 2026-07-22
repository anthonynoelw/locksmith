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
/// Integration tests for the /api/v{version}/api-key/status endpoints. Reads are identified by the
/// X-Api-Key header; the update is identified by the idempotency key in the body.
/// </summary>
[Collection("Application")]
public sealed class ApiKeyStatusEndpointTests(ApplicationFixture fixture) : ApplicationTestBase(fixture)
{
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GET_CurrentStatus_WithValidSecret_Returns200OkWithInactiveStatus()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();

        HttpResponseMessage response = await GetWithApiKeyAsync("/api/v1/api-key/status", createdKey.Secret);
        string body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ApiKeyStatusResponse? status = JsonSerializer.Deserialize<ApiKeyStatusResponse>(body, _jsonOptions);
        status.Should().NotBeNull();
        status!.Status.Should().Be("Inactive");
    }

    [Fact]
    public async Task GET_CurrentStatus_WithUnknownSecret_Returns404NotFound()
    {
        HttpResponseMessage response = await GetWithApiKeyAsync("/api/v1/api-key/status", "lk_nonexistent-secret");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GET_CurrentStatus_WithoutBearerToken_Returns401Unauthorized()
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/v1/api-key/status");

        HttpResponseMessage response = await Client.SendAsync(requestMessage);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_History_WithValidSecret_Returns200OkWithSingleInactiveEntry()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();

        HttpResponseMessage response = await GetWithApiKeyAsync("/api/v1/api-key/status/history", createdKey.Secret);
        string body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        List<ApiKeyStatusHistoryResponse>? history = JsonSerializer.Deserialize<List<ApiKeyStatusHistoryResponse>>(
            body,
            _jsonOptions);
        history.Should().HaveCount(1);
        history!.Should().ContainSingle(s => s.Status == "Inactive");
    }

    [Fact]
    public async Task GET_History_WithUnknownSecret_Returns404NotFound()
    {
        HttpResponseMessage response = await GetWithApiKeyAsync("/api/v1/api-key/status/history", "lk_nonexistent-secret");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PATCH_Update_WithValidIdempotencyKeyAndStatus_Returns200OkAndUpdatesCurrentStatus()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();

        HttpResponseMessage updateResponse = await SendUpdateAsync(createdKey.IdempotencyKey, ApiKeyStatusEnum.Active);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage statusResponse = await GetWithApiKeyAsync("/api/v1/api-key/status", createdKey.Secret);
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
        // Status validation fails before the idempotency key is ever looked up, so no key needs to exist.
        var updateRequest = new { idempotencyKey = "unused-idempotency-key" };

        HttpResponseMessage response = await SendUpdateRawAsync(updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task PATCH_Update_WithNullStatus_Returns422UnprocessableEntity()
    {
        // Status validation fails before the idempotency key is ever looked up, so no key needs to exist.
        var updateRequest = new { idempotencyKey = "unused-idempotency-key", status = (ApiKeyStatusEnum?)null };

        HttpResponseMessage response = await SendUpdateRawAsync(updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task PATCH_Update_WhenCurrentStatusIsRevoked_Returns409Conflict()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();
        HttpResponseMessage revokeResponse = await SendUpdateAsync(createdKey.IdempotencyKey, ApiKeyStatusEnum.Revoked);
        revokeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage response = await SendUpdateAsync(createdKey.IdempotencyKey, ApiKeyStatusEnum.Active);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private Task<HttpResponseMessage> SendUpdateAsync(string idempotencyKey, ApiKeyStatusEnum status) =>
        SendUpdateRawAsync(new { idempotencyKey, status = (int)status });

    private async Task<HttpResponseMessage> SendUpdateRawAsync(object updateRequest)
    {
        using var content = new StringContent(
            JsonSerializer.Serialize(updateRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/api-key/status")
        {
            Content = content,
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", BEARER_TOKEN);

        return await Client.SendAsync(requestMessage);
    }
}
