namespace Application.Endpoints.ApiKeys;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Api.Responses;
using Application.Infrastructure;
using FluentAssertions;

/// <summary>
/// Integration tests for the /api/v{version}/api-keys/{keyId}/actions endpoints.
/// </summary>
[Collection("Application")]
public sealed class ApiKeyActionEndpointTests(ApplicationFixture fixture) : ApplicationTestBase(fixture)
{
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GET_List_WithValidId_Returns200OkWithSeededActions()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync(new[] { 0, 1 });

        HttpResponseMessage response = await SendListAsync(createdKey.Id);
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
    public async Task GET_List_WithUnknownId_Returns404NotFound()
    {
        HttpResponseMessage response = await SendListAsync(Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GET_List_WithoutBearerToken_Returns401Unauthorized()
    {
        using var requestMessage = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/api-keys/{Guid.NewGuid()}/actions");

        HttpResponseMessage response = await Client.SendAsync(requestMessage);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_Grant_WithValidAction_Returns201CreatedAndActionAppearsInList()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();

        HttpResponseMessage grantResponse = await SendGrantAsync(createdKey.Id, "Write");
        grantResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage listResponse = await SendListAsync(createdKey.Id);
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

        HttpResponseMessage response = await SendGrantAsync(createdKey.Id, "Read");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task POST_Grant_WithInvalidActionName_Returns422UnprocessableEntity()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();

        HttpResponseMessage response = await SendGrantAsync(createdKey.Id, "not-an-action");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task POST_Grant_WithCommaSeparatedActionNames_Returns422UnprocessableEntity()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();

        // Enum.TryParse would OR "Write,Delete" into 3 == Execute — a privilege escalation.
        HttpResponseMessage response = await SendGrantAsync(createdKey.Id, "Write,Delete");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task POST_Grant_WithNumericActionName_Returns422UnprocessableEntity()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();

        HttpResponseMessage response = await SendGrantAsync(createdKey.Id, "3");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task POST_Grant_WithUnknownKeyId_Returns404NotFound()
    {
        HttpResponseMessage response = await SendGrantAsync(Guid.NewGuid(), "Write");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DELETE_Revoke_WithGrantedAction_Returns204NoContentAndActionRemovedFromList()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();

        HttpResponseMessage revokeResponse = await SendRevokeAsync(createdKey.Id, "Read");
        revokeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage listResponse = await SendListAsync(createdKey.Id);
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

        HttpResponseMessage response = await SendRevokeAsync(createdKey.Id, "Write");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DELETE_Revoke_WithUnknownKeyId_Returns404NotFound()
    {
        HttpResponseMessage response = await SendRevokeAsync(Guid.NewGuid(), "Read");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task POST_Grant_AfterRevoke_Returns201Created()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();
        HttpResponseMessage revokeResponse = await SendRevokeAsync(createdKey.Id, "Read");
        revokeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage reGrantResponse = await SendGrantAsync(createdKey.Id, "Read");

        reGrantResponse.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PUT_Replace_WithNewSet_Returns200OkWithReplacedActions()
    {
        CreateApiKeyResponse createdKey = await CreateApiKeyAsync();

        HttpResponseMessage response = await SendReplaceAsync(createdKey.Id, new[] { 1, 2 });
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

        HttpResponseMessage response = await SendReplaceAsync(createdKey.Id, Array.Empty<int>());
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

        HttpResponseMessage response = await SendReplaceAsync(createdKey.Id, new[] { 42 });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task PUT_Replace_WithUnknownKeyId_Returns404NotFound()
    {
        HttpResponseMessage response = await SendReplaceAsync(Guid.NewGuid(), new[] { 0 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<HttpResponseMessage> SendListAsync(Guid keyId)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/api-keys/{keyId}/actions");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-bearer-token");

        return await Client.SendAsync(requestMessage);
    }

    private async Task<HttpResponseMessage> SendGrantAsync(Guid keyId, string actionName)
    {
        using var requestMessage = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/api-keys/{keyId}/actions/{actionName}");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-bearer-token");

        return await Client.SendAsync(requestMessage);
    }

    private async Task<HttpResponseMessage> SendRevokeAsync(Guid keyId, string actionName)
    {
        using var requestMessage = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/v1/api-keys/{keyId}/actions/{actionName}");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-bearer-token");

        return await Client.SendAsync(requestMessage);
    }

    private async Task<HttpResponseMessage> SendReplaceAsync(Guid keyId, IReadOnlyList<int> actions)
    {
        using var content = new StringContent(
            JsonSerializer.Serialize(new { actions }, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/api-keys/{keyId}/actions")
        {
            Content = content,
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-bearer-token");

        return await Client.SendAsync(requestMessage);
    }
}
