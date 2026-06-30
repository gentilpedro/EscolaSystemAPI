using System.Security.Claims;
using EscolaSystemApi.Common;
using Microsoft.AspNetCore.Mvc;

namespace EscolaSystemApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    protected Guid CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
            ? id
            : Guid.Empty;

    protected IActionResult HandleResult<T>(Result<T> result) => result.StatusCode switch
    {
        200 => Ok(result.Data),
        201 => Created(string.Empty, result.Data),
        204 => NoContent(),
        400 => BadRequest(new { error = result.Error }),
        401 => Unauthorized(new { error = result.Error }),
        403 => Forbid(),
        404 => NotFound(new { error = result.Error }),
        409 => Conflict(new { error = result.Error }),
        _ => StatusCode(result.StatusCode, new { error = result.Error })
    };
}