namespace Integration.RateLimiting;

using Application.Interfaces.Services;
using Application.Settings;
using Infrastructure.RateLimiting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Testcontainers.Redis;

/// <summary>
/// Exercises the sliding-window Lua script against a live Redis container. The unit tests in
/// <c>Tests/Unit</c> only cover the backend-unavailable branches, so this is the sole coverage of
/// the actual allow/deny decision, remaining-quota arithmetic, and window reset behaviour.
/// </summary>
public sealed class RedisSlidingWindowRateLimiterTests : IAsyncLifetime
{
    private RedisContainer _container = default!;
    private IConnectionMultiplexer _multiplexer = default!;

    public async Task InitializeAsync()
    {
        _container = new RedisBuilder("redis:7.0").Build();
        await _container.StartAsync();
        _multiplexer = await ConnectionMultiplexer.ConnectAsync(_container.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await _multiplexer.CloseAsync();
        await _container.StopAsync();
    }

    [Fact]
    public async Task AcquireAsync_WhenUnderLimit_AllowsAndDecrementsRemaining()
    {
        RedisSlidingWindowRateLimiter sut = BuildSut(new RateLimitSettings { PermitLimit = 3, WindowSeconds = 60 });
        string partitionKey = Guid.NewGuid().ToString("N");

        RateLimitResult first = await sut.AcquireAsync(partitionKey);
        RateLimitResult second = await sut.AcquireAsync(partitionKey);

        first.IsAllowed.Should().BeTrue();
        first.Remaining.Should().Be(2);
        second.IsAllowed.Should().BeTrue();
        second.Remaining.Should().Be(1);
    }

    [Fact]
    public async Task AcquireAsync_WhenAtLimit_RejectsWithRetryAfter()
    {
        RedisSlidingWindowRateLimiter sut = BuildSut(new RateLimitSettings { PermitLimit = 2, WindowSeconds = 60 });
        string partitionKey = Guid.NewGuid().ToString("N");

        await sut.AcquireAsync(partitionKey);
        await sut.AcquireAsync(partitionKey);
        RateLimitResult third = await sut.AcquireAsync(partitionKey);

        third.IsAllowed.Should().BeFalse();
        third.Remaining.Should().Be(0);
        third.RetryAfter.Should().BePositive();
        third.RetryAfter.Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task AcquireAsync_WhenWindowElapses_ResetsAndAllowsAgain()
    {
        RedisSlidingWindowRateLimiter sut = BuildSut(new RateLimitSettings { PermitLimit = 1, WindowSeconds = 1 });
        string partitionKey = Guid.NewGuid().ToString("N");

        RateLimitResult first = await sut.AcquireAsync(partitionKey);
        RateLimitResult second = await sut.AcquireAsync(partitionKey);
        await Task.Delay(TimeSpan.FromSeconds(1.5));
        RateLimitResult third = await sut.AcquireAsync(partitionKey);

        first.IsAllowed.Should().BeTrue();
        second.IsAllowed.Should().BeFalse();
        third.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task AcquireAsync_DifferentPartitionKeys_AreTrackedIndependently()
    {
        RedisSlidingWindowRateLimiter sut = BuildSut(new RateLimitSettings { PermitLimit = 1, WindowSeconds = 60 });
        string keyA = Guid.NewGuid().ToString("N");
        string keyB = Guid.NewGuid().ToString("N");

        RateLimitResult first = await sut.AcquireAsync(keyA);
        RateLimitResult second = await sut.AcquireAsync(keyB);

        first.IsAllowed.Should().BeTrue();
        second.IsAllowed.Should().BeTrue();
    }

    private RedisSlidingWindowRateLimiter BuildSut(RateLimitSettings settings)
    {
        return new RedisSlidingWindowRateLimiter(
            _multiplexer,
            Options.Create(settings),
            NullLogger<RedisSlidingWindowRateLimiter>.Instance);
    }
}
