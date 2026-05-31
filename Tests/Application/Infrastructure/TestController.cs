namespace Application.Infrastructure;

using System.Collections.Generic;

using Asp.Versioning;

using Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Test-only controller that triggers specific domain exceptions to exercise the
/// exception handler pipeline during application testing. Not present in production.
/// </summary>
[ApiController]
[ApiVersionNeutral]
[Route("test")]
public sealed class TestController : ControllerBase
{
    /// <summary>Triggers a <see cref="NotFoundException"/>.</summary>
    /// <returns></returns>
    [HttpGet("not-found")]
    public IActionResult TriggerNotFound() => throw new NotFoundException("Test resource not found.");

    /// <summary>Triggers a <see cref="ConflictException"/>.</summary>
    /// <returns></returns>
    [HttpGet("conflict")]
    public IActionResult TriggerConflict() => throw new ConflictException("Duplicate test resource.");

    /// <summary>Triggers a <see cref="ValidationException"/> with a single field error.</summary>
    /// <returns></returns>
    [HttpGet("validation")]
    public IActionResult TriggerValidation() =>
        throw new ValidationException("Validation failed.", new Dictionary<string, string[]>
        {
            { "Field", ["Field is required."] },
        });

    /// <summary>Triggers an unhandled <see cref="InvalidOperationException"/>.</summary>
    /// <returns></returns>
    [HttpGet("error")]
    public IActionResult TriggerError() => throw new InvalidOperationException("Unhandled test error.");
}
