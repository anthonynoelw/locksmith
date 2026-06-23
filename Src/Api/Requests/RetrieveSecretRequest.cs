namespace Api.Requests;

using System.ComponentModel.DataAnnotations;

/// <summary>HTTP request body for retrieving and decrypting an API key secret.</summary>
public sealed record RetrieveSecretRequest
{
    /// <summary>Gets the idempotency key used to derive the encryption key for the secret.</summary>
    [Required(ErrorMessage = "Idempotency key is required.")]
    [RegularExpression(@".*\S.*", ErrorMessage = "Idempotency key cannot be empty or whitespace.")]
    public string IdempotencyKey { get; init; } = string.Empty;
}
