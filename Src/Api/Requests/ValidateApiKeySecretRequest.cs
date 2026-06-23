namespace Api.Requests;

using System.ComponentModel.DataAnnotations;

/// <summary>HTTP request body for validating an API key secret.</summary>
public sealed record ValidateApiKeySecretRequest
{
    /// <summary>Gets the API key secret to validate.</summary>
    [Required(ErrorMessage = "Secret is required.")]
    [RegularExpression(@".*\S.*", ErrorMessage = "Secret cannot be empty or whitespace.")]
    public string Secret { get; init; } = string.Empty;
}
