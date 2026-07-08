namespace Api.RateLimiting;

/// <summary>Identifies where a rate-limit partition key is read from on a request.</summary>
public enum RateLimitKeySource
{
    /// <summary>The key is a route value (for example the <c>{keyId}</c> GUID).</summary>
    Route,

    /// <summary>
    /// The key is a string property on the bound request body (for example the idempotency key or secret).
    /// The presented value is hashed for lookup so the raw credential never reaches Redis or logs.
    /// </summary>
    Body,
}
