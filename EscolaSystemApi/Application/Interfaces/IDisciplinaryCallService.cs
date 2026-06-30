using EscolaSystemApi.Application.DTOs.DisciplinaryCalls;
using EscolaSystemApi.Common;

namespace EscolaSystemApi.Application.Interfaces;

public interface IDisciplinaryCallService
{
    Task<Result<PagedResult<DisciplinaryCallDto>>> GetAllAsync(PagedQuery query, CancellationToken cancellationToken = default);
    Task<Result<DisciplinaryCallDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<DisciplinaryCallDto>> CreateAsync(CreateDisciplinaryCallDto dto, CancellationToken cancellationToken = default);
    Task<Result<DisciplinaryCallDto>> UpdateAsync(Guid id, UpdateDisciplinaryCallDto dto, CancellationToken cancellationToken = default);
    Task<Result<DisciplinaryCallDto>> ApproveAsync(Guid id, Guid resolvedById, ResolveCallDto dto, CancellationToken cancellationToken = default);
    Task<Result<DisciplinaryCallDto>> RejectAsync(Guid id, Guid resolvedById, ResolveCallDto dto, CancellationToken cancellationToken = default);
}