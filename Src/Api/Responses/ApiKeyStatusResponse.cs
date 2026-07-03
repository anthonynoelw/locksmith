namespace Api.Responses;

/// <summary>
/// Response for the current status of an API key.
/// </summary>
/// <param name="Id">The ID of the status.</param>
/// <param name="Status">The status of the API key.</param>
/// <param name="CreatedAt">The date and time the status was created.</param>
public sealed record ApiKeyStatusResponse(
    Guid Id,
    string Status,
    DateTime CreatedAt);

/// <summary>
/// Response for the history of statuses for an API key.
/// </summary>
/// <param name="Id">The ID of the status.</param>
/// <param name="Status">The status of the API key.</param>
/// <param name="CreatedAt">The date and time the status was created.</param>
/// <param name="DeletedAt">The date and time the status was deleted.</param>
public sealed record ApiKeyStatusHistoryResponse(
    Guid Id,
    string Status,
    DateTime CreatedAt,
    DateTime? DeletedAt);
