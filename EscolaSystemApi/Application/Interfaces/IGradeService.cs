using EscolaSystemApi.Application.DTOs.Grades;
using EscolaSystemApi.Common;

namespace EscolaSystemApi.Application.Interfaces;

public interface IGradeService
{
    Task<Result<PagedResult<GradeDto>>> GetAllAsync(PagedQuery query, Guid? classId = null, Guid? studentId = null, CancellationToken cancellationToken = default);
    Task<Result<GradeDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<GradeDto>> CreateAsync(CreateGradeDto dto, CancellationToken cancellationToken = default);
    Task<Result<GradeDto>> UpdateAsync(Guid id, UpdateGradeDto dto, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}