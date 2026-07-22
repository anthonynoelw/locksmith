namespace Api.Filters;

/// <summary>
/// Marks an action's successful response as cacheable by the caller's own client, scoped to the
/// specific API key that made the request, for the duration configured via
/// <see cref="Api.Settings.CacheSettings.ApiKeyReadSeconds"/>. Every action without this attribute
/// defaults to no-store via <see cref="ResponseCacheControlFilter"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CacheableAttribute : Attribute
{
}
