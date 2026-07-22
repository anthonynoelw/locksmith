namespace Application.Services;

using System.Collections.Generic;
using System.Linq;
using Domain.Enums;
using Domain.Models;

/// <summary>
/// Response-ready projection of an API key's non-sensitive metadata. The secret hash is masked and
/// the current status / active actions are already resolved so callers do no further business logic.
/// </summary>
/// <param name="Id">The API key identifier.</param>
/// <param name="MaskedSecretHash">The secret hash with all but the last few characters masked.</param>
/// <param name="CreatedAt">The date and time the API key was created.</param>
/// <param name="CreatedBy">The identity of the caller who created the API key.</param>
/// <param name="ExpiresAt">The date and time the API key expires.</param>
/// <param name="Status">The current status name (e.g. Active, Inactive, Revoked).</param>
/// <param name="Actions">The names of the actions currently granted to the API key.</param>
public sealed record ApiKeyMetadata(
    Guid Id,
    string MaskedSecretHash,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime ExpiresAt,
    string Status,
    IReadOnlyList<string> Actions);

/// <summary>
/// Builds <see cref="ApiKeyMetadata"/> from an <see cref="ApiKey"/> entity, encapsulating the
/// masking, current-status selection, and active-action filtering rules in one place.
/// </summary>
internal static class ApiKeyMetadataMapper
{
    /// <summary>Projects an API key entity to its response-ready metadata.</summary>
    /// <param name="apiKey">The API key entity, with its statuses and actions loaded.</param>
    /// <returns>The masked, resolved metadata.</returns>
    public static ApiKeyMetadata ToMetadata(ApiKey apiKey)
    {
        ApiKeyStatus? currentStatus = apiKey.Statuses
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefault();

        string statusName = currentStatus?.Status.ToString() ?? ApiKeyStatusEnum.Inactive.ToString();

        var activeActions = apiKey.Actions
            .Where(a => a.DeletedAt == null)
            .Select(a => a.Action.ToString())
            .ToList();

        return new ApiKeyMetadata(
            apiKey.Id,
            MaskSecretHash(apiKey.SecretHash),
            apiKey.CreatedAt,
            apiKey.CreatedBy,
            apiKey.ExpiresAt,
            statusName,
            activeActions);
    }

    private static string MaskSecretHash(string secretHash)
    {
        if (secretHash.Length <= 4)
        {
            return "****";
        }

        return $"****...{secretHash[^4..]}";
    }
}
