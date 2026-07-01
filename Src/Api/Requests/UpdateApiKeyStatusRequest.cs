namespace Api.Requests;

using System.ComponentModel.DataAnnotations;
using Domain.Enums;

/// <summary>
/// HTTP Request Body for updating a the API Key Status.
/// </summary>
public class UpdateApiKeyStatusRequest
{
    /// <summary>Gets the idempotency key used to update the status key for the secret.</summary>
    [Required(ErrorMessage = "Idempotency key is required.")]
    [RegularExpression(@".*\S.*", ErrorMessage = "Idempotency key cannot be empty or whitespace.")]
    public string IdempotencyKey { get; init; } = string.Empty;

    /// <summary>Gets the Status to which the secret is updated to.</summary>
    [Required(ErrorMessage = "Status is required.")]
    public ApiKeyStatusEnum Status { get; init; } = ApiKeyStatusEnum.Inactive;
}
