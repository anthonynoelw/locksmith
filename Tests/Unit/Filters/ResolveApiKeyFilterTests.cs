namespace Unit.Filters;

using Api.Filters;
using Application.Interfaces.Services;
using Domain;
using Domain.Exceptions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Moq;

public sealed class ResolveApiKeyFilterTests
{
    private readonly Mock<IGetApiKeyBySecretService> _getApiKeyBySecretService;
    private readonly Mock<ProblemDetailsFactory> _problemDetailsFactory;
    private readonly ResolveApiKeyFilter _sut;

    public ResolveApiKeyFilterTests()
    {
        _getApiKeyBySecretService = new Mock<IGetApiKeyBySecretService>();

        _problemDetailsFactory = new Mock<ProblemDetailsFactory>();
        _problemDetailsFactory
            .Setup(f => f.CreateProblemDetails(
                It.IsAny<HttpContext>(),
                It.IsAny<int?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()))
            .Returns((HttpContext _, int? status, string? _, string? _, string? _, string? _) =>
                new ProblemDetails { Status = status });

        _sut = new ResolveApiKeyFilter(_getApiKeyBySecretService.Object, _problemDetailsFactory.Object);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithoutApiKeyHeader_ShortCircuitsWith400AndDoesNotCallNext()
    {
        var httpContext = new DefaultHttpContext();
        ActionExecutingContext executingContext = BuildContext(httpContext);
        bool nextCalled = false;

        await _sut.OnActionExecutionAsync(executingContext, () =>
        {
            nextCalled = true;
            return Task.FromResult(BuildExecutedContext(httpContext));
        });

        nextCalled.Should().BeFalse();
        executingContext.Result.Should().BeOfType<BadRequestObjectResult>();
        _getApiKeyBySecretService.Verify(
            s => s.ExecuteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithDuplicateApiKeyHeader_ShortCircuitsWith400AndDoesNotResolve()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[WellKnown.RequestHeaders.API_KEY] = new[] { "lk_first", "lk_second" };
        ActionExecutingContext executingContext = BuildContext(httpContext);
        bool nextCalled = false;

        await _sut.OnActionExecutionAsync(executingContext, () =>
        {
            nextCalled = true;
            return Task.FromResult(BuildExecutedContext(httpContext));
        });

        nextCalled.Should().BeFalse();
        executingContext.Result.Should().BeOfType<BadRequestObjectResult>();
        _getApiKeyBySecretService.Verify(
            s => s.ExecuteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithValidSecret_StashesResolvedIdAndCallsNext()
    {
        Guid apiKeyId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[WellKnown.RequestHeaders.API_KEY] = "lk_secret";
        _getApiKeyBySecretService
            .Setup(s => s.ExecuteAsync("lk_secret", It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiKeyId);
        ActionExecutingContext executingContext = BuildContext(httpContext);
        bool nextCalled = false;

        await _sut.OnActionExecutionAsync(executingContext, () =>
        {
            nextCalled = true;
            return Task.FromResult(BuildExecutedContext(httpContext));
        });

        nextCalled.Should().BeTrue();
        executingContext.Result.Should().BeNull();
        httpContext.Items[WellKnown.HttpContextItems.RESOLVED_API_KEY_ID].Should().Be(apiKeyId);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenSecretUnknown_PropagatesNotFound()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[WellKnown.RequestHeaders.API_KEY] = "lk_unknown";
        _getApiKeyBySecretService
            .Setup(s => s.ExecuteAsync("lk_unknown", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("nope"));
        ActionExecutingContext executingContext = BuildContext(httpContext);

        Func<Task> act = () => _sut.OnActionExecutionAsync(
            executingContext,
            () => Task.FromResult(BuildExecutedContext(httpContext)));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static ActionExecutingContext BuildContext(HttpContext httpContext)
    {
        var actionContext = new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor());
        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller: new object());
    }

    private static ActionExecutedContext BuildExecutedContext(HttpContext httpContext)
    {
        var actionContext = new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor());
        return new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), controller: new object());
    }
}
