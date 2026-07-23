namespace Infrastructure.RateLimiting;

using Application.Interfaces.Services;
using Application.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

/// <summary>
/// A distributed sliding-window-log rate limiter backed by a Redis sorted set per partition.
/// The window is evaluated atomically by a single Lua script so the quota is enforced correctly
/// across multiple API instances. Uses the Redis server clock to avoid inter-instance clock skew.
/// </summary>
public sealed class RedisSlidingWindowRateLimiter : IRateLimiter
{
    private const string KEY_PREFIX = "ratelimit:";

    // Atomic sliding-window log:
    //   1. drop timestamps older than the window,
    //   2. count what remains,
    //   3. if under the limit, add this request and refresh the TTL,
    //   4. compute the reset from the oldest surviving entry.
    // The Redis server clock (TIME) is authoritative so all instances agree on "now".
    private const string SLIDING_WINDOW_SCRIPT = @"
local key = KEYS[1]
local window_ms = tonumber(ARGV[1])
local limit = tonumber(ARGV[2])
local member = ARGV[3]

local time = redis.call('TIME')
local now_ms = (tonumber(time[1]) * 1000) + math.floor(tonumber(time[2]) / 1000)
local window_start = now_ms - window_ms

redis.call('ZREMRANGEBYSCORE', key, 0, window_start)
local count = redis.call('ZCARD', key)

local allowed = 0
local remaining = 0

if count < limit then
    redis.call('ZADD', key, now_ms, member)
    redis.call('PEXPIRE', key, window_ms)
    count = count + 1
    allowed = 1
    remaining = limit - count
end

local reset_ms = now_ms + window_ms
local oldest = redis.call('ZRANGE', key, 0, 0, 'WITHSCORES')
if oldest[2] then
    reset_ms = tonumber(oldest[2]) + window_ms
end

return { allowed, remaining, limit, reset_ms }";

    private readonly IConnectionMultiplexer _redis;
    private readonly RateLimitSettings _settings;
    private readonly ILogger<RedisSlidingWindowRateLimiter> _logger;

    /// <summary>Initializes a new instance of the <see cref="RedisSlidingWindowRateLimiter"/> class.</summary>
    /// <param name="redis">The shared Redis connection multiplexer.</param>
    /// <param name="settings">The rate-limit configuration.</param>
    /// <param name="logger">The logger.</param>
    public RedisSlidingWindowRateLimiter(
        IConnectionMultiplexer redis,
        IOptions<RateLimitSettings> settings,
        ILogger<RedisSlidingWindowRateLimiter> logger)
    {
        _redis = redis;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<RateLimitResult> AcquireAsync(string partitionKey, CancellationToken cancellationToken = default)
    {
        int limit = _settings.PermitLimit;
        long windowMs = (long)_settings.WindowSeconds * 1000;

        try
        {
            IDatabase database = _redis.GetDatabase();
            string member = Guid.NewGuid().ToString("N");

            RedisResult raw = await database.ScriptEvaluateAsync(
                SLIDING_WINDOW_SCRIPT,
                new RedisKey[] { KEY_PREFIX + partitionKey },
                new RedisValue[] { windowMs, limit, member });

            var values = (RedisResult[])raw!;
            bool isAllowed = (long)values[0] == 1;
            long remaining = (long)values[1];
            long resultLimit = (long)values[2];
            long resetMs = (long)values[3];

            DateTimeOffset resetAt = DateTimeOffset.FromUnixTimeMilliseconds(resetMs);
            TimeSpan retryAfter = isAllowed ? TimeSpan.Zero : ClampToWindow(resetAt);

            return new RateLimitResult(isAllowed, resultLimit, remaining, resetAt, retryAfter);
        }
        catch (RedisException ex)
        {
            return HandleBackendFailure(ex, limit);
        }
    }

    private TimeSpan ClampToWindow(DateTimeOffset resetAt)
    {
        TimeSpan delta = resetAt - DateTimeOffset.UtcNow;
        if (delta <= TimeSpan.Zero)
        {
            // The window has effectively elapsed; advise a minimal, non-zero wait.
            return TimeSpan.FromSeconds(1);
        }

        return delta > TimeSpan.FromSeconds(_settings.WindowSeconds)
            ? TimeSpan.FromSeconds(_settings.WindowSeconds)
            : delta;
    }

    private RateLimitResult HandleBackendFailure(RedisException exception, int limit)
    {
        DateTimeOffset resetAt = DateTimeOffset.UtcNow.AddSeconds(_settings.WindowSeconds);

        if (_settings.FailOpen)
        {
            _logger.LogWarning(exception, "Rate limiter backend unavailable; failing open and allowing the request.");
            return new RateLimitResult(true, limit, limit - 1, resetAt, TimeSpan.Zero);
        }

        _logger.LogWarning(exception, "Rate limiter backend unavailable; failing closed and rejecting the request.");
        return new RateLimitResult(false, limit, 0, resetAt, TimeSpan.FromSeconds(_settings.WindowSeconds));
    }
}
