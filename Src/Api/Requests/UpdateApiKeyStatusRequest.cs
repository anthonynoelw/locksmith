namespace Api.Requests;

using System.ComponentModel.DataAnnotations;
using Domain.Enums;

/// <summary>
/// HTTP request body for updating an API Key status.
/// </summary>
public sealed record UpdateApiKeyStatusRequest
{
    /// <summary>Gets the idempotency key used to update the status key for the secret.</summary>
    [Required(ErrorMessage = "Idempotency key is required.")]
    [RegularExpression(@".*\S.*", ErrorMessage = "Idempotency key cannot be empty or whitespace.")]
    public string IdempotencyKey { get; init; } = string.Empty;

    /// <summary>Gets the status to which the API key is updated.</summary>
    [Required(ErrorMessage = "Status is required.")]
    public ApiKeyStatusEnum? Status { get; init; }
}
