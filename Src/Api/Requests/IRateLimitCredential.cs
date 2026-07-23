namespace Api.Requests;

/// <summary>
/// Implemented by request bodies that carry the plaintext credential (idempotency key or secret)
/// identifying the target API key, so <see cref="Api.Filters.CredentialRateLimitFilter"/> can
/// partition the rate limit on it without an extra database lookup.
/// </summary>
public interface IRateLimitCredential
{
    /// <summary>Gets the plaintext credential that identifies the target API key.</summary>
    public string RateLimitCredential { get; }
}
