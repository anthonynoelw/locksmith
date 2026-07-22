namespace Application.Endpoints.ApiKeys;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Api.Responses;
using Application.Infrastructure;
using FluentAssertions;

/// <summary>
/// Integration tests for the /api/v{version}/api-key/actions endpoints. The list read is identified
/// by the X-Api-Key header; mutations are identified by the idempotency key in the body.
/// </summary>
[Collection("Application")]
public sealed class ApiKeyActionEndpointTests(ApplicationFixture fixture) : ApplicationTestBase(fixture)
{
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GET_List_WithValidSecret_Returns200OkWithSeededActions()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync(new[] { 0, 1 });

        HttpResponseMessage response = await SendListAsync(createdKey.Secret);
        string body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        List<ApiKeyActionResponse>? actions = JsonSerializer.Deserialize<List<ApiKeyActionResponse>>(
            body,
            _jsonOptions);
        actions.Should().HaveCount(2);
        actions!.Should().ContainSingle(a => a.Action == "Read");
        actions!.Should().ContainSingle(a => a.Action == "Write");
    }

    [Fact]
    public async Task GET_List_WithUnknownSecret_Returns404NotFound()
    {
        HttpResponseMessage response = await SendListAsync("lk_nonexistent-secret");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GET_List_WithoutBearerToken_Returns401Unauthorized()
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/v1/api-key/actions");

        HttpResponseMessage response = await Client.SendAsync(requestMessage);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_Grant_WithValidAction_Returns201CreatedAndActionAppearsInList()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();

        HttpResponseMessage grantResponse = await SendGrantAsync("Write", createdKey.IdempotencyKey);
        grantResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage listResponse = await SendListAsync(createdKey.Secret);
        string listBody = await listResponse.Content.ReadAsStringAsync();
        List<ApiKeyActionResponse>? actions = JsonSerializer.Deserialize<List<ApiKeyActionResponse>>(
            listBody,
            _jsonOptions);
        actions!.Should().ContainSingle(a => a.Action == "Write");
    }

    [Fact]
    public async Task POST_Grant_WhenActionAlreadyGranted_Returns409Conflict()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();

        HttpResponseMessage response = await SendGrantAsync("Read", createdKey.IdempotencyKey);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task POST_Grant_WithInvalidActionName_Returns422UnprocessableEntity()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();

        HttpResponseMessage response = await SendGrantAsync("not-an-action", createdKey.IdempotencyKey);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task POST_Grant_WithCommaSeparatedActionNames_Returns422UnprocessableEntity()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();

        // Enum.TryParse would OR "Write,Delete" into 3 == Execute — a privilege escalation.
        HttpResponseMessage response = await SendGrantAsync("Write,Delete", createdKey.IdempotencyKey);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task POST_Grant_WithNumericActionName_Returns422UnprocessableEntity()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();

        HttpResponseMessage response = await SendGrantAsync("3", createdKey.IdempotencyKey);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task POST_Grant_WithUnknownIdempotencyKey_Returns404NotFound()
    {
        HttpResponseMessage response = await SendGrantAsync("Write", Guid.NewGuid().ToString());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DELETE_Revoke_WithGrantedAction_Returns204NoContentAndActionRemovedFromList()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();

        HttpResponseMessage revokeResponse = await SendRevokeAsync("Read", createdKey.IdempotencyKey);
        revokeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage listResponse = await SendListAsync(createdKey.Secret);
        string listBody = await listResponse.Content.ReadAsStringAsync();
        List<ApiKeyActionResponse>? actions = JsonSerializer.Deserialize<List<ApiKeyActionResponse>>(
            listBody,
            _jsonOptions);
        actions.Should().BeEmpty();
    }

    [Fact]
    public async Task DELETE_Revoke_WhenActionNotGranted_Returns404NotFound()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();

        HttpResponseMessage response = await SendRevokeAsync("Write", createdKey.IdempotencyKey);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DELETE_Revoke_WithUnknownIdempotencyKey_Returns404NotFound()
    {
        HttpResponseMessage response = await SendRevokeAsync("Read", Guid.NewGuid().ToString());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task POST_Grant_AfterRevoke_Returns201Created()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();
        HttpResponseMessage revokeResponse = await SendRevokeAsync("Read", createdKey.IdempotencyKey);
        revokeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage reGrantResponse = await SendGrantAsync("Read", createdKey.IdempotencyKey);

        reGrantResponse.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PUT_Replace_WithNewSet_Returns200OkWithReplacedActions()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();

        HttpResponseMessage response = await SendReplaceAsync(new[] { 1, 2 }, createdKey.IdempotencyKey);
        string body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        List<ApiKeyActionResponse>? actions = JsonSerializer.Deserialize<List<ApiKeyActionResponse>>(
            body,
            _jsonOptions);
        actions.Should().HaveCount(2);
        actions!.Should().ContainSingle(a => a.Action == "Write");
        actions!.Should().ContainSingle(a => a.Action == "Delete");
    }

    [Fact]
    public async Task PUT_Replace_WithEmptySet_Returns200OkAndRemovesAllActions()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();

        HttpResponseMessage response = await SendReplaceAsync(Array.Empty<int>(), createdKey.IdempotencyKey);
        string body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        List<ApiKeyActionResponse>? actions = JsonSerializer.Deserialize<List<ApiKeyActionResponse>>(
            body,
            _jsonOptions);
        actions.Should().BeEmpty();
    }

    [Fact]
    public async Task PUT_Replace_WithUndefinedActionValue_Returns422UnprocessableEntity()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();

        HttpResponseMessage response = await SendReplaceAsync(new[] { 42 }, createdKey.IdempotencyKey);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task PUT_Replace_WithUnknownIdempotencyKey_Returns404NotFound()
    {
        HttpResponseMessage response = await SendReplaceAsync(new[] { 0 }, Guid.NewGuid().ToString());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private Task<HttpResponseMessage> SendListAsync(string secret)
        => GetWithApiKeyAsync("/api/v1/api-key/actions", secret);

    private async Task<HttpResponseMessage> SendGrantAsync(string actionName, string idempotencyKey)
    {
        using var content = new StringContent(
            JsonSerializer.Serialize(new { idempotencyKey }, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        using var requestMessage = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/api-key/actions/{actionName}")
        {
            Content = content,
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", BEARER_TOKEN);

        return await Client.SendAsync(requestMessage);
    }

    private async Task<HttpResponseMessage> SendRevokeAsync(string actionName, string idempotencyKey)
    {
        using var content = new StringContent(
            JsonSerializer.Serialize(new { idempotencyKey }, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        using var requestMessage = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/v1/api-key/actions/{actionName}")
        {
            Content = content,
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", BEARER_TOKEN);

        return await Client.SendAsync(requestMessage);
    }

    private async Task<HttpResponseMessage> SendReplaceAsync(IReadOnlyList<int> actions, string idempotencyKey)
    {
        using var content = new StringContent(
            JsonSerializer.Serialize(new { idempotencyKey, actions }, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Put, "/api/v1/api-key/actions")
        {
            Content = content,
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", BEARER_TOKEN);

        return await Client.SendAsync(requestMessage);
    }
}
