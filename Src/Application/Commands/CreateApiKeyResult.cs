namespace Application.Commands;

/// <summary>Output of the CreateApiKey use case.</summary>
/// <param name="ApiKeyId">The ID of the newly created API key.</param>
/// <param name="PlaintextSecret">The plaintext API key secret — returned only on creation.</param>
/// <param name="IdempotencyKey">The idempotency key — returned only on creation; required to retrieve the secret later.</param>
public sealed record CreateApiKeyResult(
    Guid ApiKeyId,
    string PlaintextSecret,
    string IdempotencyKey);
