namespace Api.RateLimiting;

using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Applies per-API-key rate limiting to an action, declaring where the target key is read from.
/// The action is skipped when it has no target key (for example create/list).
/// </summary>
/// <remarks>
/// Implemented as an <see cref="IFilterFactory"/> so the backing <see cref="RateLimitFilter"/> can
/// resolve its dependencies (rate limiter, crypto, problem details) from the request service provider.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RateLimitAttribute : Attribute, IFilterFactory
{
    /// <summary>Initializes a new instance of the <see cref="RateLimitAttribute"/> class.</summary>
    /// <param name="source">Where the partition key is read from.</param>
    /// <param name="keyName">The route value name or request-body property name that holds the key.</param>
    public RateLimitAttribute(RateLimitKeySource source, string keyName)
    {
        Source = source;
        KeyName = keyName;
    }

    /// <summary>Gets where the partition key is read from.</summary>
    public RateLimitKeySource Source { get; }

    /// <summary>Gets the route value name or request-body property name that holds the key.</summary>
    public string KeyName { get; }

    /// <inheritdoc/>
    public bool IsReusable => false;

    /// <inheritdoc/>
    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        return ActivatorUtilities.CreateInstance<RateLimitFilter>(serviceProvider, Source, KeyName);
    }
}
