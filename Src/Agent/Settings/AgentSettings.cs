namespace Agent.Settings;

using System.ComponentModel.DataAnnotations;

/// <summary>Configuration settings for the Agent project.</summary>
public sealed class AgentSettings
{
    /// <summary>Initializes a new instance of the <see cref="AgentSettings"/> class.</summary>
    public AgentSettings()
    {
    }

    /// <summary>Gets or initializes the application name.</summary>
    [Required]
    [StringLength(256, MinimumLength = 1)]
    public required string Name { get; init; }
}
