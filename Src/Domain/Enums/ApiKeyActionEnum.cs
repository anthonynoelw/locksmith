namespace Domain.Enums;

/// <summary>
/// Represents the valid actions of an API key.
/// </summary>
public enum ApiKeyActionEnum
{
    /// <summary>
    /// The action to read an existing resource.
    /// </summary>
    Read,

    /// <summary>
    /// The action to write (create or update) a resource.
    /// </summary>
    Write,

    /// <summary>
    /// The action to delete an existing resource.
    /// </summary>
    Delete,

    /// <summary>
    /// The action to execute a command.
    /// </summary>
    Execute,
}
