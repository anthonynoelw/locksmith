namespace Application.Infrastructure;

using global::Application.Interfaces.Services;

/// <summary>
/// A deterministic <see cref="IRateLimiter"/> for tests that always returns a preconfigured result,
/// letting HTTP-level tests exercise the allow/reject paths without a live Redis backend.
/// </summary>
public sealed class FakeRateLimiter : IRateLimiter
{
    private readonly RateLimitResult _result;

    /// <summary>Initializes a new instance of the <see cref="FakeRateLimiter"/> class.</summary>
    /// <param name="result">The fixed result returned for every acquisition.</param>
    public FakeRateLimiter(RateLimitResult result)
    {
        _result = result;
    }

    /// <inheritdoc/>
    public Task<RateLimitResult> AcquireAsync(string partitionKey, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_result);
    }
}
