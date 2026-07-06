namespace Api.Requests;

using Domain.Enums;

/// <summary>HTTP request body for replacing the action set of an API key.</summary>
public sealed record UpdateApiKeyActionsRequest
{
    /// <summary>Gets the desired set of granted actions. An empty list revokes all actions.</summary>
    public IReadOnlyList<ApiKeyActionEnum> Actions { get; init; } = new List<ApiKeyActionEnum>();
}
