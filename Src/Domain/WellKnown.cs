namespace Domain;

/// <summary>Shared string constants used across layers to avoid magic strings.</summary>
public static class WellKnown
{
    /// <summary>
    /// Configuration section names for options binding.
    /// Use with <c>AddOptions&lt;T&gt;().BindConfiguration(ConfigSections.X).ValidateDataAnnotations().ValidateOnStart()</c>.
    /// </summary>
    public static class ConfigSections
    {
        /// <summary>The "API" configuration section (API project settings).</summary>
        public const string API = "API";

        /// <summary>The "AGENT" configuration section (AGENT project settings).</summary>
        public const string AGENT = "AGENT";

        /// <summary>The "Cryptography" configuration section (crypto/key-derivation settings).</summary>
        public const string CRYPTOGRAPHY = "Cryptography";
    }

    /// <summary>
    /// Connection string keys used to retrieve values from the configuration.
    /// Reference these when calling <c>configuration.GetConnectionString(ConnectionStringKeys.X)</c>.
    /// </summary>
    public static class ConnectionStringKeys
    {
        /// <summary>The default database connection string (Postgres).</summary>
        public const string DEFAULT = "DefaultConnection";

        /// <summary>The Redis connection string for caching and distributed state.</summary>
        public const string REDIS = "Redis";
    }

    /// <summary>
    /// Tag constants used to categorise health checks by probe type.
    /// Attach these when registering infrastructure checks:
    /// <code>
    /// builder.Services.AddHealthChecks()
    ///     .AddDbContextCheck&lt;AppDbContext&gt;(tags: [HealthCheckTags.Ready]);
    /// </code>
    /// </summary>
    public static class HealthCheckTags
    {
        /// <summary>Marks a check as a readiness dependency. Runs under <c>GET /health/ready</c>.</summary>
        public const string READY = "READY";
    }

    /// <summary>
    /// Authentication scheme names. Use these when registering and referencing authentication handlers.
    /// </summary>
    public static class AuthenticationSchemes
    {
        /// <summary>Bearer token authentication scheme for static pre-shared token validation.</summary>
        public const string BEARER = "Bearer";
    }

    /// <summary>
    /// Well-known custom request header names.
    /// </summary>
    public static class RequestHeaders
    {
        /// <summary>
        /// Header carrying the caller's plaintext API key secret. On read (GET) endpoints it identifies
        /// which API key the request is about; it is also the partition source for future rate limiting.
        /// </summary>
        public const string API_KEY = "X-Api-Key";
    }

    /// <summary>
    /// Keys used to stash per-request values on <c>HttpContext.Items</c>.
    /// </summary>
    public static class HttpContextItems
    {
        /// <summary>The identifier of the API key resolved from the <see cref="RequestHeaders.API_KEY"/> header.</summary>
        public const string RESOLVED_API_KEY_ID = "ResolvedApiKeyId";
    }

    /// <summary>
    /// Rate-limiting policy names. Reserved for the future limiter that partitions by the resolved
    /// API key; not yet wired into a limiter.
    /// </summary>
    public static class RateLimitPolicies
    {
        /// <summary>Per-API-key rate-limit policy, partitioned by the caller's API key.</summary>
        public const string PER_API_KEY = "per-api-key";
    }

    /// <summary>
    /// Caller identities recorded in <c>CreatedBy</c> audit columns. Audit columns are persisted and
    /// exposed in API responses, so they must never contain secrets such as the bearer token itself.
    /// </summary>
    public static class CallerIdentities
    {
        /// <summary>The identity recorded for requests authenticated with the pre-shared bearer token.</summary>
        public const string API_CLIENT = "api-client";
    }

    /// <summary>
    /// Durations, in seconds, for <c>[Cacheable]</c>-marked actions. Their responses vary per caller
    /// (see <see cref="RequestHeaders.API_KEY"/>), so these are private, per-key cache lifetimes only.
    /// </summary>
    public static class CacheDurations
    {
        /// <summary>How long a caller's own client may reuse a read-only API key response.</summary>
        public const int API_KEY_READ_SECONDS = 60;
    }
}
