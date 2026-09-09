namespace Api.Requests;

using System.ComponentModel.DataAnnotations;
using Domain.Enums;

/// <summary>HTTP request body for replacing the action set of an API key.</summary>
public sealed record UpdateApiKeyActionsRequest : IRateLimitCredential
{
    /// <summary>Gets the idempotency key used to identify the API key.</summary>
    [Required(ErrorMessage = "Idempotency key is required.")]
    [RegularExpression(@".*\S.*", ErrorMessage = "Idempotency key cannot be empty or whitespace.")]
    public string IdempotencyKey { get; init; } = string.Empty;

    /// <summary>Gets the desired set of granted actions. An empty list revokes all actions.</summary>
    public IReadOnlyList<ApiKeyActionEnum> Actions { get; init; } = new List<ApiKeyActionEnum>();

    /// <inheritdoc/>
    string IRateLimitCredential.RateLimitCredential => IdempotencyKey;
}
