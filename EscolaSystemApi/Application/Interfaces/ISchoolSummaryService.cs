using EscolaSystemApi.Application.DTOs.Schools;
using EscolaSystemApi.Common;

namespace EscolaSystemApi.Application.Interfaces;

public interface ISchoolSummaryService
{
    // Todas as escolas, com os números para apontar implantação incompleta
    Task<Result<IReadOnlyList<SchoolOverviewDto>>> GetOverviewAsync(CancellationToken cancellationToken = default);
    // Uma escola: contato da direção e contagens
    Task<Result<SchoolSummaryDto>> GetSummaryAsync(Guid schoolId, CancellationToken cancellationToken = default);
}
