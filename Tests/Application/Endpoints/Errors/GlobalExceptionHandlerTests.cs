namespace Application.Endpoints.Errors;

using System.Net;
using System.Net.Http.Json;

using Application.Infrastructure;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Application tests verifying that <see cref="Api.Exceptions.GlobalExceptionHandler"/>
/// returns RFC 9457-compliant responses over real HTTP for each domain exception type.
/// </summary>
public sealed class GlobalExceptionHandlerTests(ApplicationFixture fixture) : ApplicationTestBase(fixture)
{
    [Fact]
    public async Task GET_TestNotFound_WhenNotFoundExceptionIsThrown_Returns404ProblemDetails()
    {
        // Arrange — TestController throws NotFoundException at /test/not-found

        // Act
        var response = await Client.GetAsync("/test/not-found");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(404);
        problem.Title.Should().Be("Not Found");
        problem.Detail.Should().Be("Test resource not found.");
        problem.Instance.Should().Be("/test/not-found");
    }

    [Fact]
    public async Task GET_TestConflict_WhenConflictExceptionIsThrown_Returns409ProblemDetails()
    {
        // Arrange — TestController throws ConflictException at /test/conflict

        // Act
        var response = await Client.GetAsync("/test/conflict");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(409);
        problem.Title.Should().Be("Conflict");
        problem.Detail.Should().Be("Duplicate test resource.");
    }

    [Fact]
    public async Task GET_TestValidation_WhenValidationExceptionIsThrown_Returns422ValidationProblemDetails()
    {
        // Arrange — TestController throws ValidationException at /test/validation

        // Act
        var response = await Client.GetAsync("/test/validation");
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(422);
        problem.Title.Should().Be("Unprocessable Entity");
        problem.Errors.Should().ContainKey("Field");
    }

    [Fact]
    public async Task GET_TestError_WhenUnhandledExceptionIsThrown_Returns500WithExceptionDetail()
    {
        // Arrange — TestController throws InvalidOperationException at /test/error (Development exposes detail)

        // Act
        var response = await Client.GetAsync("/test/error");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        problem.Should().NotBeNull();
        problem!.Detail.Should().Be("Unhandled test error.");
    }
}
