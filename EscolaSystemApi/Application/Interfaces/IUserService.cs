using EscolaSystemApi.Application.DTOs.Users;
using EscolaSystemApi.Common;

namespace EscolaSystemApi.Application.Interfaces;

public interface IUserService
{
    Task<Result<PagedResult<UserListDto>>> GetAllAsync(PagedQuery query, Guid? schoolId = null, int? roleId = null, string? search = null, bool? isActive = null, CancellationToken cancellationToken = default);
    Task<Result<UserListDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<UserListDto>> CreateAsync(CreateUserDto dto, CancellationToken cancellationToken = default);
    Task<Result<UserListDto>> UpdateAsync(Guid id, UpdateUserDto dto, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<bool>> AssignClassAsync(Guid teacherId, Guid classId, CancellationToken cancellationToken = default);
    Task<Result<bool>> UnassignClassAsync(Guid teacherId, Guid classId, CancellationToken cancellationToken = default);
    Task<Result<bool>> AssignStudentAsync(Guid parentId, Guid studentId, CancellationToken cancellationToken = default);
    Task<Result<bool>> UnassignStudentAsync(Guid parentId, Guid studentId, CancellationToken cancellationToken = default);
    Task<Result<bool>> AssignOrientadorClassAsync(Guid orientadorId, Guid classId, CancellationToken cancellationToken = default);
    Task<Result<bool>> UnassignOrientadorClassAsync(Guid orientadorId, Guid classId, CancellationToken cancellationToken = default);
}
