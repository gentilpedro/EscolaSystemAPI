using EscolaSystemApi.Application.DTOs.Dashboard;
using EscolaSystemApi.Common;

namespace EscolaSystemApi.Application.Interfaces;

public interface IDashboardService
{
    Task<Result<AdminStatsDto>> GetAdminStatsAsync(CancellationToken cancellationToken = default);
    Task<Result<DashboardStatsDto>> GetStatsAsync(CancellationToken cancellationToken = default);
    Task<Result<List<ClassReportDto>>> GetClassReportsAsync(Guid? schoolId = null, CancellationToken cancellationToken = default);
}
