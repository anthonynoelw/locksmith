namespace Unit.RateLimiting;

using Application.Interfaces.Services;
using Application.Settings;
using FluentAssertions;
using Infrastructure.RateLimiting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

public sealed class RedisSlidingWindowRateLimiterTests
{
    [Fact]
    public async Task AcquireAsync_WhenBackendUnavailable_AndFailOpen_AllowsRequest()
    {
        RedisSlidingWindowRateLimiter sut = BuildSut(
            new RateLimitSettings { PermitLimit = 100, WindowSeconds = 60, FailOpen = true });

        RateLimitResult result = await sut.AcquireAsync("some-key");

        result.IsAllowed.Should().BeTrue();
        result.Limit.Should().Be(100);
        result.Remaining.Should().Be(99);
    }

    [Fact]
    public async Task AcquireAsync_WhenBackendUnavailable_AndFailClosed_RejectsRequest()
    {
        RedisSlidingWindowRateLimiter sut = BuildSut(
            new RateLimitSettings { PermitLimit = 100, WindowSeconds = 60, FailOpen = false });

        RateLimitResult result = await sut.AcquireAsync("some-key");

        result.IsAllowed.Should().BeFalse();
        result.Remaining.Should().Be(0);
        result.RetryAfter.Should().Be(TimeSpan.FromSeconds(60));
    }

    private static RedisSlidingWindowRateLimiter BuildSut(RateLimitSettings settings)
    {
        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer
            .Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "backend down"));

        return new RedisSlidingWindowRateLimiter(
            multiplexer.Object,
            Options.Create(settings),
            NullLogger<RedisSlidingWindowRateLimiter>.Instance);
    }
}
