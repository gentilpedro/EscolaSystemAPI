using System.Security.Claims;
using EscolaSystemApi.Common;
using FluentValidation.Results;
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

    // Sessão do token de acesso (claim "sid"; o handler JWT pode mapeá-la para ClaimTypes.Sid)
    protected Guid? CurrentSessionId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.Sid) ?? User.FindFirstValue("sid"), out var id) ? id : null;

    // "message" acompanha "error" para que o front exiba o motivo da falha
    protected IActionResult HandleResult<T>(Result<T> result) => result.StatusCode switch
    {
        200 => Ok(result.Data),
        201 => Created(string.Empty, result.Data),
        204 => NoContent(),
        _ => StatusCode(result.StatusCode, new { error = result.Error, message = result.Error })
    };

    protected IActionResult ValidationFailed(ValidationResult validation)
    {
        var errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
        return BadRequest(new { error = errors.First(), message = string.Join(" ", errors), errors });
    }
}
