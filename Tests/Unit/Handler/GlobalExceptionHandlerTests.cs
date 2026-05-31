namespace Unit.Handler;

using System.Collections.Generic;

using Api.Exceptions;
using Domain.Exceptions;
using FluentAssertions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

public sealed class GlobalExceptionHandlerTests
{
    private readonly Mock<IProblemDetailsService> _problemDetailsService;
    private readonly Mock<IWebHostEnvironment> _environment;
    private readonly GlobalExceptionHandler _handler;

    public GlobalExceptionHandlerTests()
    {
        _problemDetailsService = new Mock<IProblemDetailsService>();
        _environment = new Mock<IWebHostEnvironment>();

        _environment.Setup(e => e.EnvironmentName).Returns("Production");

        _handler = new GlobalExceptionHandler(
            _problemDetailsService.Object,
            _environment.Object,
            NullLogger<GlobalExceptionHandler>.Instance);
    }

    private static HttpContext BuildHttpContext(string path = "/api/test")
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = "GET";
        return context;
    }

    [Fact]
    public async Task TryHandleAsync_NotFoundException_Sets404StatusCode()
    {
        var context = BuildHttpContext();
        var exception = new NotFoundException("Order 42 not found.");

        await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task TryHandleAsync_NotFoundException_ReturnsTrueIndicatingHandled()
    {
        var context = BuildHttpContext();
        var exception = new NotFoundException("Order 42 not found.");

        bool handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.Should().BeTrue();
    }

    [Fact]
    public async Task TryHandleAsync_NotFoundException_WritesProblemDetailsWithCorrectShape()
    {
        var context = BuildHttpContext("/api/orders/42");
        var exception = new NotFoundException("Order 42 not found.");
        ProblemDetails? capturedDetails = null;

        _problemDetailsService
            .Setup(s => s.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
            .Callback<ProblemDetailsContext>(ctx => capturedDetails = ctx.ProblemDetails);

        await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        capturedDetails.Should().NotBeNull();
        capturedDetails!.Status.Should().Be(StatusCodes.Status404NotFound);
        capturedDetails.Title.Should().Be("Not Found");
        capturedDetails.Detail.Should().Be("Order 42 not found.");
        capturedDetails.Instance.Should().Be("/api/orders/42");
    }

    [Fact]
    public async Task TryHandleAsync_ConflictException_Sets409StatusCode()
    {
        var context = BuildHttpContext();
        var exception = new ConflictException("Duplicate email address.");

        await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task TryHandleAsync_ConflictException_WritesProblemDetailsWithCorrectTitle()
    {
        var context = BuildHttpContext();
        var exception = new ConflictException("Duplicate email address.");
        ProblemDetails? capturedDetails = null;

        _problemDetailsService
            .Setup(s => s.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
            .Callback<ProblemDetailsContext>(ctx => capturedDetails = ctx.ProblemDetails);

        await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        capturedDetails!.Title.Should().Be("Conflict");
        capturedDetails.Detail.Should().Be("Duplicate email address.");
    }

    [Fact]
    public async Task TryHandleAsync_ValidationException_Sets422StatusCode()
    {
        var context = BuildHttpContext();
        var exception = new ValidationException("Validation failed.", new Dictionary<string, string[]>
        {
            { "Email", ["Email is required.", "Email must be valid."] },
            { "Price", ["Price must be positive."] },
        });

        await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        context.Response.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task TryHandleAsync_ValidationException_WritesValidationProblemDetailsWithErrors()
    {
        var context = BuildHttpContext();
        var errors = new Dictionary<string, string[]>
        {
            { "Email", ["Email is required."] },
            { "Price", ["Price must be positive."] },
        };
        var exception = new ValidationException("Validation failed.", errors);
        ProblemDetails? capturedDetails = null;

        _problemDetailsService
            .Setup(s => s.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
            .Callback<ProblemDetailsContext>(ctx => capturedDetails = ctx.ProblemDetails);

        await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        var validationDetails = capturedDetails.Should().BeOfType<ValidationProblemDetails>().Subject;
        validationDetails.Errors.Should().HaveCount(2);
        validationDetails.Errors.Keys.Should().Contain("Email");
        validationDetails.Errors.Keys.Should().Contain("Price");
    }

    [Fact]
    public async Task TryHandleAsync_UnexpectedException_Sets500StatusCode()
    {
        var context = BuildHttpContext();
        var exception = new InvalidOperationException("Internal state corruption.");

        await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task TryHandleAsync_UnexpectedException_InProduction_RedactsDetail()
    {
        _environment.Setup(e => e.EnvironmentName).Returns("Production");
        var context = BuildHttpContext();
        var exception = new InvalidOperationException("Sensitive internal message.");
        ProblemDetails? capturedDetails = null;

        _problemDetailsService
            .Setup(s => s.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
            .Callback<ProblemDetailsContext>(ctx => capturedDetails = ctx.ProblemDetails);

        await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        capturedDetails.Should().NotBeNull();
        (capturedDetails?.Detail ?? string.Empty).Should().NotContain("Sensitive internal message.");
        capturedDetails!.Detail.Should().Be("An unexpected error occurred. Please try again later.");
    }

    [Fact]
    public async Task TryHandleAsync_UnexpectedException_InDevelopment_ExposesDetail()
    {
        _environment.Setup(e => e.EnvironmentName).Returns("Development");
        var context = BuildHttpContext();
        var exception = new InvalidOperationException("Sensitive internal message.");
        ProblemDetails? capturedDetails = null;

        _problemDetailsService
            .Setup(s => s.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
            .Callback<ProblemDetailsContext>(ctx => capturedDetails = ctx.ProblemDetails);

        await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        capturedDetails!.Detail.Should().Be("Sensitive internal message.");
    }

    [Fact]
    public async Task TryHandleAsync_AnyException_AlwaysReturnsTrue()
    {
        var context = BuildHttpContext();
        var exceptions = new Exception[]
        {
            new NotFoundException("not found"),
            new ConflictException("conflict"),
            new ValidationException("invalid"),
            new InvalidOperationException("unexpected"),
            new ArgumentException("arg"),
        };

        foreach (var exception in exceptions)
        {
            bool handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);
            handled.Should().BeTrue($"Handler returned false for {exception.GetType().Name}");
        }
    }

    [Fact]
    public async Task TryHandleAsync_NotFoundException_InProduction_ExposesDetailUnredacted()
    {
        _environment.Setup(e => e.EnvironmentName).Returns("Production");
        var context = BuildHttpContext();
        var exception = new NotFoundException("Order 99 not found.");
        ProblemDetails? capturedDetails = null;

        _problemDetailsService
            .Setup(s => s.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
            .Callback<ProblemDetailsContext>(ctx => capturedDetails = ctx.ProblemDetails);

        await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        capturedDetails!.Detail.Should().Be("Order 99 not found.");
    }
}
