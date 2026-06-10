namespace Domain.Enums;

/// <summary>
/// Represents the status of an API key.
/// </summary>
public enum ApiKeyStatusEnum
{
    /// <summary>
    /// The API key has been created but not yet activated.
    /// </summary>
    Inactive,

    /// <summary>
    /// The API key has been activated.
    /// </summary>
    Active,

    /// <summary>
    /// The API key has been revoked.
    /// </summary>
    Revoked,

    /// <summary>
    /// The API key has expired.
    /// </summary>
    Expired,
}
