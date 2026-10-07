using EscolaSystemApi.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EscolaSystemApi.Controllers;

// Números por escola para a administração: contagens e o contato da direção, sem dados pessoais da escola
[Authorize(Roles = "Admin")]
[Route("api/admin/schools")]
public class AdminSchoolsController(ISchoolSummaryService summaryService) : BaseApiController
{
    [HttpGet("summary")]
    public async Task<IActionResult> Overview(CancellationToken cancellationToken)
        => HandleResult(await summaryService.GetOverviewAsync(cancellationToken));

    [HttpGet("{id:guid}/summary")]
    public async Task<IActionResult> Summary(Guid id, CancellationToken cancellationToken)
        => HandleResult(await summaryService.GetSummaryAsync(id, cancellationToken));
}
