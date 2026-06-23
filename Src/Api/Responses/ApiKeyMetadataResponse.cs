namespace Api.Responses;

using Domain.Enums;

/// <summary>Metadata for an existing API key.</summary>
public sealed record ApiKeyMetadataResponse(
    Guid Id,
    string MaskedSecretHash,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime ExpiresAt,
    string Status,
    IReadOnlyList<string> Actions);
