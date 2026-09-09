namespace Application.Interfaces.Services;

/// <summary>
/// Enforces a sliding-window request quota for a single logical partition (one managed API key).
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Records an attempt against the given partition and reports whether it is permitted.
    /// </summary>
    /// <param name="partitionKey">A stable identity for the API key being operated on.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The outcome of the attempt, including remaining quota and reset timing.</returns>
    public Task<RateLimitResult> AcquireAsync(string partitionKey, CancellationToken cancellationToken = default);
}

/// <summary>The result of a single rate-limit acquisition attempt.</summary>
/// <param name="IsAllowed">Whether the request is within quota and may proceed.</param>
/// <param name="Limit">The maximum number of requests permitted in the window.</param>
/// <param name="Remaining">The number of requests remaining in the current window.</param>
/// <param name="ResetAt">The instant at which the current window resets.</param>
/// <param name="RetryAfter">How long the caller should wait before retrying; <see cref="TimeSpan.Zero"/> when allowed.</param>
public sealed record RateLimitResult(
    bool IsAllowed,
    long Limit,
    long Remaining,
    DateTimeOffset ResetAt,
    TimeSpan RetryAfter);
