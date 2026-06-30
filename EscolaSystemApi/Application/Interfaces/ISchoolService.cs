using EscolaSystemApi.Application.DTOs.Schools;
using EscolaSystemApi.Common;

namespace EscolaSystemApi.Application.Interfaces;

public interface ISchoolService
{
    Task<Result<PagedResult<SchoolDto>>> GetAllAsync(PagedQuery query, CancellationToken cancellationToken = default);
    Task<Result<SchoolDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<SchoolDto>> CreateAsync(CreateSchoolDto dto, CancellationToken cancellationToken = default);    
    Task<Result<SchoolDto>> UpdateAsync(Guid id, UpdateSchoolDto dto, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
