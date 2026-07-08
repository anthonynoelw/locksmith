namespace Unit.RateLimiting;

using Api.RateLimiting;
using Api.Requests;
using Application.Interfaces.Services;
using Application.Settings;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Moq;

public sealed class RateLimitFilterTests
{
    private static readonly RateLimitResult _allowed =
        new (true, 100, 99, DateTimeOffset.FromUnixTimeSeconds(1_700_000_060), TimeSpan.Zero);

    [Fact]
    public async Task RouteKey_WhenAllowed_SetsQuotaHeaders_AndCallsNext()
    {
        var limiter = new Mock<IRateLimiter>();
        limiter.Setup(l => l.AcquireAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_allowed);

        Guid keyId = Guid.NewGuid();
        (ActionExecutingContext context, HttpContext http) = BuildContext(
            new Dictionary<string, object?> { ["id"] = keyId });
        (ActionExecutionDelegate next, Func<bool> wasCalled) = BuildNext(context);

        RateLimitFilter sut = BuildSut(limiter.Object, Mock.Of<ICryptoService>(), RateLimitKeySource.Route, "id");
        await sut.OnActionExecutionAsync(context, next);

        wasCalled().Should().BeTrue();
        context.Result.Should().BeNull();
        limiter.Verify(l => l.AcquireAsync(keyId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        http.Response.Headers["X-RateLimit-Limit"].ToString().Should().Be("100");
        http.Response.Headers["X-RateLimit-Remaining"].ToString().Should().Be("99");
        http.Response.Headers["X-RateLimit-Reset"].ToString().Should().Be("1700000060");
    }

    [Fact]
    public async Task RouteKey_WhenRejected_ShortCircuitsWith429_RetryAfter_AndProblemDetails()
    {
        var rejected = new RateLimitResult(false, 100, 0, DateTimeOffset.UtcNow.AddSeconds(30), TimeSpan.FromSeconds(30));
        var limiter = new Mock<IRateLimiter>();
        limiter.Setup(l => l.AcquireAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rejected);

        (ActionExecutingContext context, HttpContext http) = BuildContext(
            new Dictionary<string, object?> { ["id"] = Guid.NewGuid() });
        (ActionExecutionDelegate next, Func<bool> wasCalled) = BuildNext(context);

        RateLimitFilter sut = BuildSut(limiter.Object, Mock.Of<ICryptoService>(), RateLimitKeySource.Route, "id");
        await sut.OnActionExecutionAsync(context, next);

        wasCalled().Should().BeFalse();
        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        var problem = result.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Be("Too Many Requests");
        problem.Status.Should().Be(StatusCodes.Status429TooManyRequests);
        http.Response.Headers.RetryAfter.ToString().Should().Be("30");
    }

    [Fact]
    public async Task BodyKey_HashesPresentedCredential_ForPartition()
    {
        var crypto = new Mock<ICryptoService>();
        crypto.Setup(c => c.HashForLookup("the-secret")).Returns("HASHED");
        var limiter = new Mock<IRateLimiter>();
        limiter.Setup(l => l.AcquireAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_allowed);

        (ActionExecutingContext context, _) = BuildContext(new Dictionary<string, object?>
        {
            ["request"] = new ValidateApiKeySecretRequest { Secret = "the-secret" },
        });
        (ActionExecutionDelegate next, _) = BuildNext(context);

        RateLimitFilter sut = BuildSut(
            limiter.Object, crypto.Object, RateLimitKeySource.Body, nameof(ValidateApiKeySecretRequest.Secret));
        await sut.OnActionExecutionAsync(context, next);

        limiter.Verify(l => l.AcquireAsync("HASHED", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WhenDisabled_PassesThrough_WithoutCallingLimiter()
    {
        var limiter = new Mock<IRateLimiter>();

        (ActionExecutingContext context, HttpContext http) = BuildContext(
            new Dictionary<string, object?> { ["id"] = Guid.NewGuid() });
        (ActionExecutionDelegate next, Func<bool> wasCalled) = BuildNext(context);

        RateLimitFilter sut = BuildSut(
            limiter.Object,
            Mock.Of<ICryptoService>(),
            RateLimitKeySource.Route,
            "id",
            new RateLimitSettings { Enabled = false });
        await sut.OnActionExecutionAsync(context, next);

        wasCalled().Should().BeTrue();
        limiter.Verify(l => l.AcquireAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        http.Response.Headers.Should().NotContainKey("X-RateLimit-Limit");
    }

    [Fact]
    public async Task WhenTargetKeyMissing_PassesThrough_WithoutCallingLimiter()
    {
        var limiter = new Mock<IRateLimiter>();

        (ActionExecutingContext context, _) = BuildContext(new Dictionary<string, object?>());
        (ActionExecutionDelegate next, Func<bool> wasCalled) = BuildNext(context);

        RateLimitFilter sut = BuildSut(limiter.Object, Mock.Of<ICryptoService>(), RateLimitKeySource.Route, "id");
        await sut.OnActionExecutionAsync(context, next);

        wasCalled().Should().BeTrue();
        limiter.Verify(l => l.AcquireAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static RateLimitFilter BuildSut(
        IRateLimiter limiter,
        ICryptoService crypto,
        RateLimitKeySource source,
        string keyName,
        RateLimitSettings? settings = null)
    {
        return new RateLimitFilter(
            limiter,
            crypto,
            Options.Create(settings ?? new RateLimitSettings()),
            source,
            keyName);
    }

    private static (ActionExecutingContext Context, HttpContext Http) BuildContext(
        IDictionary<string, object?> actionArguments)
    {
        var http = new DefaultHttpContext();
        var actionContext = new ActionContext(
            http,
            new RouteData(),
            new ActionDescriptor(),
            new ModelStateDictionary());

        var context = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            actionArguments,
            controller: new object());

        return (context, http);
    }

    private static (ActionExecutionDelegate Next, Func<bool> WasCalled) BuildNext(ActionExecutingContext context)
    {
        bool called = false;
        ActionExecutionDelegate next = () =>
        {
            called = true;
            return Task.FromResult(new ActionExecutedContext(
                context,
                new List<IFilterMetadata>(),
                controller: new object()));
        };

        return (next, () => called);
    }
}
