namespace Application.Interfaces.Services;

/// <summary>
/// Validates an API key secret and returns its current status.
/// </summary>
public interface IValidateApiKeySecretService
{
    /// <summary>Validates an API key secret and returns its current status.</summary>
    /// <param name="secret">The plaintext secret to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result containing API key ID, validity, and status.</returns>
    public Task<ValidateApiKeySecretResult> ExecuteAsync(string secret, CancellationToken cancellationToken = default);
}

/// <summary>Result of validating an API key secret.</summary>
/// <param name="ApiKeyId">The API key identifier.</param>
/// <param name="IsValid">Whether the API key is in Active status.</param>
/// <param name="Status">The current status string (Active, Inactive, Revoked, etc.).</param>
public sealed record ValidateApiKeySecretResult(Guid ApiKeyId, bool IsValid, string Status);
