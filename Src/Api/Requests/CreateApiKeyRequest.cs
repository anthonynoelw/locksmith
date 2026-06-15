namespace Api.Requests;

using Domain.Enums;

/// <summary>HTTP request body for creating a new API key.</summary>
public sealed record CreateApiKeyRequest
{
    /// <summary>Gets the optional expiry date for the key. Defaults to 30 days from creation when omitted.</summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>Gets the actions to grant on the new key.</summary>
    public IReadOnlyList<ApiKeyActionEnum> Actions { get; init; } = new List<ApiKeyActionEnum>();
}
