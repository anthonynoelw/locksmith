namespace Api.Authentication;

using Microsoft.AspNetCore.Authentication;

/// <summary>
/// Options for bearer token authentication. The actual token is resolved from <see cref="ApiSettings"/>
/// rather than stored here, so this class is intentionally empty.
/// </summary>
public sealed class BearerTokenAuthenticationOptions : AuthenticationSchemeOptions
{
}
