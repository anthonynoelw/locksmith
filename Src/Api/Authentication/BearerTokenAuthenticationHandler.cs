namespace Api.Authentication;

using System.Security.Cryptography;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Api.Settings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

/// <summary>
/// Authentication handler that validates bearer tokens against a static pre-shared secret.
/// Tokens are compared using timing-safe equality to prevent timing attacks.
/// </summary>
public sealed class BearerTokenAuthenticationHandler(
    IOptionsMonitor<BearerTokenAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptionsMonitor<ApiSettings> apiSettings)
    : AuthenticationHandler<BearerTokenAuthenticationOptions>(options, logger, encoder)
{
    private const string BEARER_PREFIX = "Bearer ";

    /// <summary>
    /// Validates the bearer token from the Authorization header against the configured token.
    /// </summary>
    /// <returns>An authentication result indicating success, no result, or failure.</returns>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string headerValue = authorizationHeader.ToString();

        if (!headerValue.StartsWith(BEARER_PREFIX, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid authorization header format."));
        }

        string token = headerValue[BEARER_PREFIX.Length..];

        if (string.IsNullOrEmpty(token))
        {
            return Task.FromResult(AuthenticateResult.Fail("Bearer token cannot be empty."));
        }

        string configuredToken = apiSettings.CurrentValue.BearerToken;
        byte[] tokenBytes = System.Text.Encoding.UTF8.GetBytes(token);
        byte[] configuredTokenBytes = System.Text.Encoding.UTF8.GetBytes(configuredToken);

        if (!CryptographicOperations.FixedTimeEquals(tokenBytes, configuredTokenBytes))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid bearer token."));
        }

        var claims = new List<Claim>
        {
            new (ClaimTypes.AuthenticationMethod, Scheme.Name),
        };

        ClaimsIdentity identity = new (claims, Scheme.Name);
        ClaimsPrincipal principal = new (identity);
        AuthenticationTicket ticket = new (principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    /// <summary>
    /// Sets the WWW-Authenticate response header when authentication fails.
    /// </summary>
    /// <param name="properties">The authentication properties.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override Task HandleChallengeAsync(AuthenticationProperties? properties)
    {
        Response.Headers.Append("WWW-Authenticate", "Bearer");
        return base.HandleChallengeAsync(properties ?? new AuthenticationProperties());
    }
}
