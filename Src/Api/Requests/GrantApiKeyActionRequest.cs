namespace Api.Requests;

using System.ComponentModel.DataAnnotations;

/// <summary>HTTP request body for granting an action to an API key.</summary>
public sealed record GrantApiKeyActionRequest : IRateLimitCredential
{
    /// <summary>Gets the idempotency key used to identify the API key.</summary>
    [Required(ErrorMessage = "Idempotency key is required.")]
    [RegularExpression(@".*\S.*", ErrorMessage = "Idempotency key cannot be empty or whitespace.")]
    public string IdempotencyKey { get; init; } = string.Empty;

    /// <inheritdoc/>
    string IRateLimitCredential.RateLimitCredential => IdempotencyKey;
}
