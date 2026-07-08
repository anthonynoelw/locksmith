namespace Unit.Services.Status;

using Domain.Enums;
using Domain.Models;

/// <summary>Shared test-data builders for the API Key status service unit tests.</summary>
internal static class StatusTestData
{
    /// <summary>Builds an <see cref="ApiKeyStatus"/> for the given API key ID and status.</summary>
    /// <param name="apiKeyId">The API key ID the status belongs to.</param>
    /// <param name="status">The status value.</param>
    /// <returns>
    /// An <see cref="ApiKeyStatus"/> wrapping a matching <see cref="ApiKey"/> built via
    /// <see cref="ApiKeyTestData.BuildApiKey"/>.
    /// </returns>
    public static ApiKeyStatus BuildStatus(Guid apiKeyId, ApiKeyStatusEnum status) =>
        new ()
        {
            Id = Guid.NewGuid(),
            ApiKeyId = apiKeyId,
            Status = status,
            ApiKey = ApiKeyTestData.BuildApiKey(apiKeyId),
        };
}
