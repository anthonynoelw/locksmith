namespace Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Base controller for all versioned API endpoints. Carries the cross-cutting concerns that apply to
/// every endpoint so they are declared once rather than per action.
/// </summary>
/// <remarks>
/// Every request must present the static bearer token (<see cref="AuthorizeAttribute"/>). When per-API-key
/// rate limiting lands, its policy attaches here as
/// <c>[EnableRateLimiting(WellKnown.RateLimitPolicies.PER_API_KEY)]</c>, partitioned on the API key
/// resolved from the <c>X-Api-Key</c> header (see <see cref="Api.Filters.ResolveApiKeyFilter"/>).
/// </remarks>
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize]
public abstract class Controller : ControllerBase
{
}
