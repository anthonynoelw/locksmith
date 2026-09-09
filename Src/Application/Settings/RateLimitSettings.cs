namespace Application.Settings;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Configuration for the per-API-key sliding-window rate limiter.
/// Bound from the <c>RateLimiting</c> configuration section and validated at startup.
/// </summary>
public sealed class RateLimitSettings
{
    /// <summary>Gets a value indicating whether rate limiting is enforced. When <see langword="false"/>, requests pass through untouched.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Gets the maximum number of requests permitted per API key within <see cref="WindowSeconds"/>.</summary>
    [Range(1, 1_000_000)]
    public int PermitLimit { get; init; } = 100;

    /// <summary>Gets the length of the sliding window, in seconds.</summary>
    [Range(1, 86_400)]
    public int WindowSeconds { get; init; } = 60;

    /// <summary>
    /// Gets a value indicating whether requests are allowed when the Redis backend is unavailable.
    /// <see langword="true"/> favours availability (fail open); <see langword="false"/> enforces strictly (fail closed).
    /// </summary>
    public bool FailOpen { get; init; } = true;
}
