using EscolaSystemApi.Application.DTOs.Users;
using EscolaSystemApi.Common;

namespace EscolaSystemApi.Application.Interfaces;

public interface IUserService
{
    Task<Result<PagedResult<UserListDto>>> GetAllAsync(PagedQuery query, Guid? schoolId = null, int? roleId = null, string? search = null, bool? isActive = null, bool? locked = null, string? sort = null, CancellationToken cancellationToken = default);
    Task<Result<UserListDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<UserListDto>> CreateAsync(CreateUserDto dto, CancellationToken cancellationToken = default);
    Task<Result<UserListDto>> UpdateAsync(Guid id, UpdateUserDto dto, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<bool>> UnlockAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<bool>> RevokeSessionsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<bool>> AssignClassAsync(Guid teacherId, Guid classId, CancellationToken cancellationToken = default);
    Task<Result<bool>> UnassignClassAsync(Guid teacherId, Guid classId, CancellationToken cancellationToken = default);
    Task<Result<bool>> AssignStudentAsync(Guid parentId, Guid studentId, CancellationToken cancellationToken = default);
    Task<Result<bool>> UnassignStudentAsync(Guid parentId, Guid studentId, CancellationToken cancellationToken = default);
    Task<Result<bool>> AssignOrientadorClassAsync(Guid orientadorId, Guid classId, CancellationToken cancellationToken = default);
    Task<Result<bool>> UnassignOrientadorClassAsync(Guid orientadorId, Guid classId, CancellationToken cancellationToken = default);
    // Pessoa já cadastrada entra em outra escola (professor, orientador ou responsável)
    Task<Result<UserListDto>> AddMemberAsync(Guid schoolId, string email, CancellationToken cancellationToken = default);
    // Sai da escola sem apagar: encerra os vínculos com ela, as turmas e os alunos dela
    Task<Result<bool>> RemoveMemberAsync(Guid schoolId, Guid userId, CancellationToken cancellationToken = default);
}
