namespace Unit.Filters;

using Api.Filters;
using Application.Interfaces.Services;
using Application.Settings;
using Domain;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Moq;

public sealed class RateLimitFilterTests
{
    private static readonly RateLimitResult _allowed =
        new (true, 100, 99, DateTimeOffset.FromUnixTimeSeconds(1_700_000_060), TimeSpan.Zero);

    [Fact]
    public async Task OnActionExecutionAsync_WhenAllowed_PartitionsByResolvedApiKeyIdAndSetsQuotaHeaders()
    {
        var limiter = new Mock<IRateLimiter>();
        limiter.Setup(l => l.AcquireAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_allowed);

        Guid apiKeyId = Guid.NewGuid();
        (ActionExecutingContext context, HttpContext http) = BuildContext(apiKeyId);
        (ActionExecutionDelegate next, Func<bool> wasCalled) = BuildNext(context);

        RateLimitFilter sut = BuildSut(limiter.Object);
        await sut.OnActionExecutionAsync(context, next);

        wasCalled().Should().BeTrue();
        context.Result.Should().BeNull();
        limiter.Verify(l => l.AcquireAsync(apiKeyId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        http.Response.Headers[WellKnown.RateLimitHeaders.LIMIT].ToString().Should().Be("100");
        http.Response.Headers[WellKnown.RateLimitHeaders.REMAINING].ToString().Should().Be("99");
        http.Response.Headers[WellKnown.RateLimitHeaders.RESET].ToString().Should().Be("1700000060");
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenRejected_ShortCircuitsWith429AndProblemDetails()
    {
        var rejected = new RateLimitResult(false, 100, 0, DateTimeOffset.UtcNow.AddSeconds(30), TimeSpan.FromSeconds(30));
        var limiter = new Mock<IRateLimiter>();
        limiter.Setup(l => l.AcquireAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rejected);

        (ActionExecutingContext context, HttpContext http) = BuildContext(Guid.NewGuid());
        (ActionExecutionDelegate next, Func<bool> wasCalled) = BuildNext(context);

        RateLimitFilter sut = BuildSut(limiter.Object);
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
    public async Task OnActionExecutionAsync_WhenDisabled_PassesThroughWithoutCallingLimiter()
    {
        var limiter = new Mock<IRateLimiter>();

        (ActionExecutingContext context, HttpContext http) = BuildContext(Guid.NewGuid());
        (ActionExecutionDelegate next, Func<bool> wasCalled) = BuildNext(context);

        RateLimitFilter sut = BuildSut(limiter.Object, new RateLimitSettings { Enabled = false });
        await sut.OnActionExecutionAsync(context, next);

        wasCalled().Should().BeTrue();
        limiter.Verify(l => l.AcquireAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        http.Response.Headers.Should().NotContainKey(WellKnown.RateLimitHeaders.LIMIT);
    }

    private static RateLimitFilter BuildSut(IRateLimiter limiter, RateLimitSettings? settings = null)
    {
        return new RateLimitFilter(limiter, Options.Create(settings ?? new RateLimitSettings()));
    }

    private static (ActionExecutingContext Context, HttpContext Http) BuildContext(Guid resolvedApiKeyId)
    {
        var http = new DefaultHttpContext();
        http.Items[WellKnown.HttpContextItems.RESOLVED_API_KEY_ID] = resolvedApiKeyId;

        var actionContext = new ActionContext(http, new RouteData(), new ControllerActionDescriptor());
        var context = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
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
