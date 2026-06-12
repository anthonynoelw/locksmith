namespace Api.Settings;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Database configuration settings.
/// </summary>
public class DatabaseSettings
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SECTION_NAME = "Database";

    /// <summary>
    /// Gets or sets the database user name.
    /// </summary>
    [Required]
    public string User { get; set; } = string.Empty;
}
