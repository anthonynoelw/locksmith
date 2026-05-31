namespace Api.Controllers;

using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Base controller for all versioned API endpoints.
/// </summary>
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
public abstract class Controller : ControllerBase
{
}
