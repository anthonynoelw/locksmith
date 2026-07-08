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
    /// Caller identities recorded in <c>CreatedBy</c> audit columns. Audit columns are persisted and
    /// exposed in API responses, so they must never contain secrets such as the bearer token itself.
    /// </summary>
    public static class CallerIdentities
    {
        /// <summary>The identity recorded for requests authenticated with the pre-shared bearer token.</summary>
        public const string API_CLIENT = "api-client";
    }
}
