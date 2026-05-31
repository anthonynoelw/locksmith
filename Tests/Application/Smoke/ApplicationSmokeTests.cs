namespace Application.Smoke;

using System.Net;

using Application.Infrastructure;

/// <summary>
/// Smoke tests that verify the application boots successfully and the middleware
/// pipeline responds correctly to basic requests.
/// </summary>
public sealed class ApplicationSmokeTests(ApplicationFixture fixture) : ApplicationTestBase(fixture)
{
    [Fact]
    public async Task GET_OpenApi_WhenEnvironmentIsDevelopment_Returns200()
    {
        // Act
        var response = await Client.GetAsync("/openapi/v1.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GET_UnknownRoute_WhenNoEndpointMatched_Returns404()
    {
        // Act
        var response = await Client.GetAsync("/api/does-not-exist");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
