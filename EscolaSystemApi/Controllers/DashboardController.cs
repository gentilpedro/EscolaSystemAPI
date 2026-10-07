using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EscolaSystemApi.Controllers;

[Authorize]
[Route("api")]
public class DashboardController(IDashboardService dashboardService) : BaseApiController
{
    [HttpGet("admin/stats")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAdminStats(CancellationToken cancellationToken)
        => HandleResult(await dashboardService.GetAdminStatsAsync(cancellationToken));

    [HttpGet("dashboard/stats")]
    [Authorize(Roles = RoleNames.School)]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
        => HandleResult(await dashboardService.GetStatsAsync(cancellationToken));

    [HttpGet("reports/classes")]
    [Authorize(Roles = "Director,Teacher,Orientador")]
    public async Task<IActionResult> GetClassReports([FromQuery] Guid? schoolId, CancellationToken cancellationToken)
        => HandleResult(await dashboardService.GetClassReportsAsync(schoolId, cancellationToken));
}
