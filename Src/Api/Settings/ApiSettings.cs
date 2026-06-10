namespace Api.Settings;

using System.ComponentModel.DataAnnotations;

/// <summary>Configuration settings for the API project.</summary>
public sealed class ApiSettings
{
    /// <summary>Initializes a new instance of the <see cref="ApiSettings"/> class.</summary>
    public ApiSettings()
    {
    }

    /// <summary>Gets or initializes the application name.</summary>
    [Required]
    [StringLength(256, MinimumLength = 1)]
    public required string Name { get; init; }

    /// <summary>Gets or initializes the bearer token for the API.</summary>
    [Required]
    [StringLength(256, MinimumLength = 1)]
    public required string BearerToken { get; init; }
}
