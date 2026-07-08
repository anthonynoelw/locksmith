namespace Unit.Services;

using Domain.Models;

/// <summary>Shared test-data builders used across the API Key service unit tests.</summary>
internal static class ApiKeyTestData
{
    /// <summary>Builds an <see cref="ApiKey"/> with the given ID and otherwise-arbitrary field values.</summary>
    /// <param name="apiKeyId">The ID to assign to the built key.</param>
    /// <returns>An <see cref="ApiKey"/> suitable for use in mock setups and fixtures.</returns>
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
}
