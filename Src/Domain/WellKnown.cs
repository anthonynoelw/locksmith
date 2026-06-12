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
}
