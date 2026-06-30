using EscolaSystemApi.Application.DTOs.Students;
using EscolaSystemApi.Common;

namespace EscolaSystemApi.Application.Interfaces;

public interface IStudentService
{
    Task<Result<PagedResult<StudentDto>>> GetAllAsync(PagedQuery query, Guid? classId = null, CancellationToken cancellationToken = default);
    Task<Result<StudentDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<StudentDto>> CreateAsync(CreateStudentDto dto, CancellationToken cancellationToken = default);
    Task<Result<StudentDto>> UpdateAsync(Guid id, UpdateStudentDto dto, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}