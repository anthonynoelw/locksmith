namespace Unit.Services.Actions;

using Domain.Enums;
using Domain.Models;

/// <summary>Shared test-data builders for the API Key action service unit tests.</summary>
internal static class ActionsTestData
{
    /// <summary>Builds an <see cref="ApiKeyAction"/> for the given API key ID and action.</summary>
    /// <param name="apiKeyId">The API key ID the action belongs to.</param>
    /// <param name="action">The action value.</param>
    /// <param name="deletedAt">The optional soft-delete timestamp; null builds an active grant.</param>
    /// <returns>
    /// An <see cref="ApiKeyAction"/> wrapping a matching <see cref="ApiKey"/> built via
    /// <see cref="ApiKeyTestData.BuildApiKey"/>.
    /// </returns>
    public static ApiKeyAction BuildAction(Guid apiKeyId, ApiKeyActionEnum action, DateTime? deletedAt = null) =>
        new ()
        {
            Id = Guid.NewGuid(),
            ApiKeyId = apiKeyId,
            Action = action,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "caller",
            DeletedAt = deletedAt,
            ApiKey = ApiKeyTestData.BuildApiKey(apiKeyId),
        };
}
