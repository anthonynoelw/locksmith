namespace Api.Requests;

using System.ComponentModel.DataAnnotations;

/// <summary>HTTP request body for revoking an action from an API key.</summary>
public sealed record RevokeApiKeyActionRequest
{
    /// <summary>Gets the idempotency key used to identify the API key.</summary>
    [Required(ErrorMessage = "Idempotency key is required.")]
    [RegularExpression(@".*\S.*", ErrorMessage = "Idempotency key cannot be empty or whitespace.")]
    public string IdempotencyKey { get; init; } = string.Empty;
}
