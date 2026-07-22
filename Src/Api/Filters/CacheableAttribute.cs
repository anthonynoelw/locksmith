namespace Api.Filters;

/// <summary>
/// Marks an action's successful response as cacheable by the caller's own client, scoped to the
/// specific API key that made the request. Every action without this attribute defaults to
/// no-store via <see cref="ResponseCacheControlFilter"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CacheableAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="CacheableAttribute"/> class.</summary>
    /// <param name="maxAgeSeconds">How long the caller's own client may reuse the response, in seconds.</param>
    public CacheableAttribute(int maxAgeSeconds)
    {
        MaxAgeSeconds = maxAgeSeconds;
    }

    /// <summary>Gets how long the caller's own client may reuse the response, in seconds.</summary>
    public int MaxAgeSeconds { get; }
}
