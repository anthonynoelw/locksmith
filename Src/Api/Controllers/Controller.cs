namespace Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Base controller for all versioned API endpoints. Carries the cross-cutting concerns that apply to
/// every endpoint so they are declared once rather than per action.
/// </summary>
/// <remarks>
/// Every request must present the static bearer token (<see cref="AuthorizeAttribute"/>). Per-API-key
/// rate limiting is opt-in per action via <c>[ServiceFilter(typeof(Api.Filters.RateLimitFilter))]</c>,
/// partitioned on the API key resolved from the <c>X-Api-Key</c> header (see
/// <see cref="Api.Filters.ResolveApiKeyFilter"/>) — it must run after that filter on the same action.
/// </remarks>
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize]
public abstract class Controller : ControllerBase
{
}
