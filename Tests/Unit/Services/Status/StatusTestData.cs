namespace Unit.Services.Status;

using Domain.Enums;
using Domain.Models;

/// <summary>Shared test-data builders for the API Key status service unit tests.</summary>
internal static class StatusTestData
{
    /// <summary>Builds an <see cref="ApiKey"/> with the given ID and otherwise-arbitrary field values.</summary>
    /// <param name="apiKeyId">The ID to assign to the built key.</param>
    /// <returns>An <see cref="ApiKey"/> suitable for use in mock setups and status/idempotency-key fixtures.</returns>
    public static ApiKey BuildApiKey(Guid apiKeyId) =>
        new ()
        {
            Id = apiKeyId,
            Secret = "encrypted",
            SecretHash = "hash",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "caller",
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            Statuses = new List<ApiKeyStatus>(),
            Actions = new List<ApiKeyAction>(),
        };

    /// <summary>Builds an <see cref="ApiKeyStatus"/> for the given API key ID and status.</summary>
    /// <param name="apiKeyId">The API key ID the status belongs to.</param>
    /// <param name="status">The status value.</param>
    /// <returns>
    /// An <see cref="ApiKeyStatus"/> wrapping a matching <see cref="ApiKey"/> built via <see cref="BuildApiKey"/>.
    /// </returns>
    public static ApiKeyStatus BuildStatus(Guid apiKeyId, ApiKeyStatusEnum status) =>
        new ()
        {
            Id = Guid.NewGuid(),
            ApiKeyId = apiKeyId,
            Status = status,
            ApiKey = BuildApiKey(apiKeyId),
        };
}
