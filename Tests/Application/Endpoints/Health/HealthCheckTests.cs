namespace Application.Endpoints.Health;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Application.Infrastructure;

/// <summary>
/// Application tests verifying that the health check endpoints respond correctly.
/// </summary>
public sealed class HealthCheckTests(ApplicationFixture fixture) : ApplicationTestBase(fixture)
{
    [Fact]
    public async Task GET_Health_WhenAppIsRunning_Returns200()
    {
        var response = await Client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GET_Health_WhenAppIsRunning_ReturnsHealthyStatus()
    {
        var response = await Client.GetAsync("/health");
        var body = await response.Content.ReadFromJsonAsync<JsonDocument>();

        body!.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
    }

    [Fact]
    public async Task GET_HealthReady_WhenNoChecksRegistered_Returns200()
    {
        var response = await Client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GET_HealthReady_WhenNoChecksRegistered_ReturnsHealthyStatus()
    {
        var response = await Client.GetAsync("/health/ready");
        var body = await response.Content.ReadFromJsonAsync<JsonDocument>();

        body!.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
    }
}
