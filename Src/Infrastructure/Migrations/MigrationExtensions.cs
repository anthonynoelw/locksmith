namespace Infrastructure.Migrations;

using Microsoft.Extensions.Configuration;

/// <summary>
/// Extension methods for migrations to access configuration.
/// </summary>
public static class MigrationExtensions
{
    /// <summary>
    /// Gets the database user from configuration or environment.
    /// </summary>
    /// <returns>The database user name, or "postgres" if not available.</returns>
    public static string GetDatabaseUser()
    {
        // Try environment variable first
        string? envUser = Environment.GetEnvironmentVariable("DB_USER");
        if (!string.IsNullOrEmpty(envUser))
        {
            return envUser;
        }

        // Try configuration
        try
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../Api"))
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .Build();

            string? configUser = configuration.GetSection("Database:User").Value;
            if (!string.IsNullOrEmpty(configUser))
            {
                return configUser;
            }
        }
        catch
        {
            // Configuration not available, continue to default
        }

        return "postgres";
    }
}