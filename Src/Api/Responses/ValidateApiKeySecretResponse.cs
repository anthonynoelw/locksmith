namespace Api.Responses;

/// <summary>HTTP response body for API key secret validation.</summary>
public sealed record ValidateApiKeySecretResponse(
    Guid ApiKeyId,
    bool IsValid,
    string Status);
