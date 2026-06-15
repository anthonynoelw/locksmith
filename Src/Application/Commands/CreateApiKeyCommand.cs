namespace Application.Commands;

using Domain.Enums;

/// <summary>Input to the CreateApiKey use case.</summary>
/// <param name="CreatedBy">Identity of the caller creating the key.</param>
/// <param name="ExpiresAt">Optional expiry; defaults to 30 days from now when omitted.</param>
/// <param name="Actions">Actions to grant on the new key.</param>
public sealed record CreateApiKeyCommand(
    string CreatedBy,
    DateTime? ExpiresAt,
    IReadOnlyList<ApiKeyActionEnum> Actions);
