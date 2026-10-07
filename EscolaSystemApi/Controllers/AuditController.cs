using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EscolaSystemApi.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/audit")]
public class AuditController(IAuditService auditService) : BaseApiController
{
    // to é exclusivo: from=2026-10-01&to=2026-10-02 traz o dia 1º inteiro
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] PagedQuery query, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? action, [FromQuery] Guid? actorId, [FromQuery] Guid? targetId, CancellationToken cancellationToken)
        => HandleResult(await auditService.GetAsync(query, from, to, action, actorId, targetId, cancellationToken));
}
