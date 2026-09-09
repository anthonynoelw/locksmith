namespace Api.Requests;

using System.ComponentModel.DataAnnotations;

/// <summary>HTTP request body for validating an API key secret.</summary>
public sealed record UpdateApiKeyRequest : IRateLimitCredential
{
    /// <summary>Gets the idempotency key used to update the status key for the secret.</summary>
    [Required(ErrorMessage = "Idempotency key is required.")]
    [RegularExpression(@".*\S.*", ErrorMessage = "Idempotency key cannot be empty or whitespace.")]
    public string IdempotencyKey { get; init; } = string.Empty;

    /// <inheritdoc/>
    string IRateLimitCredential.RateLimitCredential => IdempotencyKey;
}
