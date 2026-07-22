namespace Api.Settings;

using System.ComponentModel.DataAnnotations;

/// <summary>Cache-control durations for <c>[Cacheable]</c>-marked actions.</summary>
public sealed class CacheSettings
{
    /// <summary>Gets how long, in seconds, a caller's own client may reuse a read-only API key response.</summary>
    [Range(0, 86400)]
    public int ApiKeyReadSeconds { get; init; } = 60;
}
