namespace Api.Responses;

/// <summary>
/// Response for a granted action of an API key.
/// </summary>
/// <param name="Id">The ID of the action grant.</param>
/// <param name="Action">The granted action.</param>
/// <param name="CreatedAt">The date and time the action was granted.</param>
public sealed record ApiKeyActionResponse(
    Guid Id,
    string Action,
    DateTime CreatedAt);
