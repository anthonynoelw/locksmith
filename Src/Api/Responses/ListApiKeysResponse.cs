namespace Api.Responses;

/// <summary>HTTP response body for listing API keys with pagination.</summary>
public sealed record ListApiKeysResponse(
    IReadOnlyList<ApiKeyMetadataResponse> Items,
    int Total,
    int Limit,
    int Offset);
