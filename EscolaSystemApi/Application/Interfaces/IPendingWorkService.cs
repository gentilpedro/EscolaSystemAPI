using EscolaSystemApi.Application.DTOs.PendingWorks;
using EscolaSystemApi.Common;

namespace EscolaSystemApi.Application.Interfaces;

public interface IPendingWorkService
{
    Task<Result<PagedResult<PendingWorkDto>>> GetAllAsync(PagedQuery query, Guid? classId = null, Guid? studentId = null, CancellationToken cancellationToken = default);
    Task<Result<PendingWorkDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<PendingWorkDto>> CreateAsync(CreatePendingWorkDto dto, CancellationToken cancellationToken = default);
    Task<Result<PendingWorkDto>> MarkAsDeliveredAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<ClassAssignmentDto>> CreateForClassAsync(CreateClassAssignmentDto dto, CancellationToken cancellationToken = default);
    Task<Result<ClassAssignmentDto>> UpdateAssignmentAsync(Guid assignmentId, UpdateAssignmentDto dto, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken = default);
}