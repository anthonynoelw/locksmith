namespace Application.Infrastructure;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Api.Responses;
using FluentAssertions;

/// <summary>
/// Base class for application-level tests. Participates in the "Application" collection
/// so the shared <see cref="ApplicationFixture"/> is injected by xUnit.
/// </summary>
[Collection("Application")]
public abstract class ApplicationTestBase
{
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationTestBase"/> class.
    /// </summary>
    /// <param name="fixture">The application fixture.</param>
    protected ApplicationTestBase(ApplicationFixture fixture)
    {
        Client = fixture.Client;
        Services = fixture.Services;
    }

    /// <summary>Gets the HTTP client targeting the in-process application.</summary>
    protected HttpClient Client { get; }

    /// <summary>Gets the root service provider of the running application.</summary>
    protected IServiceProvider Services { get; }

    /// <summary>Creates an API key over HTTP and returns the parsed response, for use as test setup.</summary>
    /// <param name="actions">The action codes to grant on the new key. Defaults to Read (0).</param>
    /// <returns>The created key's ID, plaintext secret, and idempotency key.</returns>
    protected async Task<CreateApiKeyResponse> CreateApiKeyAsync(IReadOnlyList<int>? actions = null)
    {
        object request = new { actions = actions ?? new[] { 0 } };
        using var content = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            Encoding.UTF8,
            "application/json");

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
}
