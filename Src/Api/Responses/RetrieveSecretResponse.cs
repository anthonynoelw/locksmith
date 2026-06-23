namespace Api.Responses;

/// <summary>HTTP response body for retrieving and decrypting an API key secret.</summary>
public sealed record RetrieveSecretResponse(
    Guid ApiKeyId,
    string Secret);
