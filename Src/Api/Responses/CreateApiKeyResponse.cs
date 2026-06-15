namespace Api.Responses;

/// <summary>HTTP response body for a newly created API key.</summary>
/// <remarks>
/// <para>
/// <see cref="Secret"/> and <see cref="IdempotencyKey"/> are returned only once at creation time.
/// Store both values securely — they cannot be retrieved again.
/// <see cref="IdempotencyKey"/> is required to retrieve <see cref="Secret"/> via the get-secret endpoint.
/// </para>
/// </remarks>
public sealed record CreateApiKeyResponse(
    Guid Id,
    string Secret,
    string IdempotencyKey);
