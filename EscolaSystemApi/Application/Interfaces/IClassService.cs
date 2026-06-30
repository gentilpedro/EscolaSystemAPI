using EscolaSystemApi.Application.DTOs.Classes;
using EscolaSystemApi.Common;

namespace EscolaSystemApi.Application.Interfaces;

public interface IClassService
{
    Task<Result<PagedResult<ClassDto>>> GetAllAsync(PagedQuery query, CancellationToken cancellationToken = default);
    Task<Result<ClassDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<ClassDto>> CreateAsync(CreateClassDto dto, CancellationToken cancellationToken = default);
    Task<Result<ClassDto>> UpdateAsync(Guid id, UpdateClassDto dto, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}