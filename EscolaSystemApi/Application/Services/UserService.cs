using EscolaSystemApi.Application.DTOs.Users;
using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Application.Interfaces.Repositories;
using EscolaSystemApi.Common;
using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscolaSystemApi.Application.Services;

public class UserService(
    IUnitOfWork unitOfWork,
    AppDbContext context,
    ICurrentUserService currentUser,
    ICpfEncryptionService cpfEncryption) : IUserService
{
    private bool IsDirector => currentUser.Role == "Director";

    public async Task<Result<PagedResult<UserListDto>>> GetAllAsync(PagedQuery query, Guid? schoolId = null, int? roleId = null, string? search = null, bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var filtered = ScopedUsers();

        if (schoolId.HasValue)
            filtered = filtered.Where(u => u.SchoolId == schoolId.Value);
        if (roleId.HasValue)
            filtered = filtered.Where(u => u.RoleId == roleId.Value);
        if (isActive.HasValue)
            filtered = filtered.Where(u => u.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            // Trecho do nome ou do e-mail, sem diferenciar maiúsculas
            var term = search.Trim().ToLower();
            filtered = filtered.Where(u => u.Name.ToLower().Contains(term) || u.Email.ToLower().Contains(term));
        }

        var totalCount = await filtered.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);
        var data = await filtered
            .OrderBy(u => u.Name)
            .Skip(query.Skip).Take(query.Take)
            .ToListAsync(cancellationToken);

        return Result<PagedResult<UserListDto>>.Success(
            new PagedResult<UserListDto>(data.Select(ToDto), query.Page, query.PageSize, totalCount, totalPages));
    }

    public async Task<Result<UserListDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await ScopedUsers().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        return user is null
            ? Result<UserListDto>.NotFound("Usuário não encontrado.")
            : Result<UserListDto>.Success(ToDto(user));
    }

    public async Task<Result<UserListDto>> CreateAsync(CreateUserDto dto, CancellationToken cancellationToken = default)
    {
        // Hierarquia: Admin cria Admin/Diretor; Diretor cria os perfis da própria escola
        if (IsDirector)
        {
            if (!RoleIds.SchoolMembers.Contains(dto.RoleId))
                return Result<UserListDto>.Forbidden("Diretor só pode criar Professor, Aluno, Responsável ou Orientador.");

            if (dto.SchoolId != currentUser.SchoolId)
                return Result<UserListDto>.Forbidden("Você só pode criar usuários na sua escola.");
        }
        else if (!RoleIds.Platform.Contains(dto.RoleId))
        {
            return Result<UserListDto>.Forbidden(
                "Administrador cria apenas Administradores e Diretores. Os demais perfis são criados pelo diretor da escola.");
        }

        var schoolCheck = await ValidateSchoolForRoleAsync(dto.RoleId, dto.SchoolId, cancellationToken);
        if (schoolCheck is not null)
            return schoolCheck;

        if (!await context.Roles.AnyAsync(r => r.Id == dto.RoleId, cancellationToken))
            return Result<UserListDto>.NotFound("Role não encontrada.");

        if (await context.Users.AnyAsync(u => u.Email == dto.Email, cancellationToken))
            return Result<UserListDto>.Conflict("E-mail já cadastrado.");

        if (!string.IsNullOrWhiteSpace(dto.Cpf))
        {
            var cpfHash = cpfEncryption.Hash(dto.Cpf);
            if (await context.Users.AnyAsync(u => u.CpfHash == cpfHash, cancellationToken))
                return Result<UserListDto>.Conflict("CPF já cadastrado.");
        }

        if (dto.RoleId == RoleIds.Student)
        {
            var studentCheck = await ValidateStudentLinkAsync(dto.StudentId, dto.SchoolId, null, cancellationToken);
            if (studentCheck is not null)
                return studentCheck;
        }

        var user = new User
        {
            Name = dto.Name,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            RoleId = dto.RoleId,
            SchoolId = dto.RoleId == RoleIds.Admin ? null : dto.SchoolId,
            StudentId = dto.RoleId == RoleIds.Student ? dto.StudentId : null,
            CpfEncrypted = string.IsNullOrWhiteSpace(dto.Cpf) ? null : cpfEncryption.Encrypt(dto.Cpf),
            CpfHash = string.IsNullOrWhiteSpace(dto.Cpf) ? null : cpfEncryption.Hash(dto.Cpf),
            Phone = dto.Phone
        };

        await unitOfWork.Repository<User>().AddAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(user.Id, cancellationToken) is { IsSuccess: true } r
            ? Result<UserListDto>.Created(r.Data!)
            : Result<UserListDto>.BadRequest("Erro ao criar usuário.");
    }

    public async Task<Result<UserListDto>> UpdateAsync(Guid id, UpdateUserDto dto, CancellationToken cancellationToken = default)
    {
        var user = await context.Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user is null)
            return Result<UserListDto>.NotFound("Usuário não encontrado.");

        var isSelf = user.Id == currentUser.UserId;

        if (IsDirector)
        {
            if (user.SchoolId != currentUser.SchoolId)
                return Result<UserListDto>.Forbidden("Você não tem permissão para editar este usuário.");

            if (!isSelf && !RoleIds.SchoolMembers.Contains(user.RoleId))
                return Result<UserListDto>.Forbidden("Você não pode editar administradores ou outros diretores.");

            var allowedRoles = isSelf ? [RoleIds.Director] : RoleIds.SchoolMembers;
            if (!allowedRoles.Contains(dto.RoleId))
                return Result<UserListDto>.Forbidden("Você não pode atribuir este perfil.");

            if (dto.SchoolId != currentUser.SchoolId)
                return Result<UserListDto>.Forbidden("Você não pode mover usuários para outra escola.");
        }
        else if (dto.RoleId != user.RoleId && !RoleIds.Platform.Contains(dto.RoleId))
        {
            // Mesma regra da criação: perfis da escola são atribuídos pelo diretor.
            // Manter o perfil atual (ex.: ativar/desativar) continua permitido.
            return Result<UserListDto>.Forbidden(
                "Administrador atribui apenas os perfis Administrador e Diretor. Os demais perfis são definidos pelo diretor da escola.");
        }

        if (isSelf && !dto.IsActive)
            return Result<UserListDto>.BadRequest("Você não pode desativar sua própria conta.");

        var schoolCheck = await ValidateSchoolForRoleAsync(dto.RoleId, dto.SchoolId, cancellationToken);
        if (schoolCheck is not null)
            return schoolCheck;

        if (!await context.Roles.AnyAsync(r => r.Id == dto.RoleId, cancellationToken))
            return Result<UserListDto>.NotFound("Role não encontrada.");

        if (await context.Users.AnyAsync(u => u.Email == dto.Email && u.Id != id, cancellationToken))
            return Result<UserListDto>.Conflict("E-mail já cadastrado.");

        var studentId = dto.RoleId == RoleIds.Student ? dto.StudentId ?? user.StudentId : null;
        if (dto.RoleId == RoleIds.Student)
        {
            var studentCheck = await ValidateStudentLinkAsync(studentId, dto.SchoolId, user.Id, cancellationToken);
            if (studentCheck is not null)
                return studentCheck;
        }

        // Cpf nulo mantém o atual; string vazia remove
        if (dto.Cpf is not null)
        {
            if (string.IsNullOrWhiteSpace(dto.Cpf))
            {
                user.CpfEncrypted = null;
                user.CpfHash = null;
            }
            else
            {
                var cpfHash = cpfEncryption.Hash(dto.Cpf);
                if (await context.Users.AnyAsync(u => u.CpfHash == cpfHash && u.Id != id, cancellationToken))
                    return Result<UserListDto>.Conflict("CPF já cadastrado.");

                user.CpfEncrypted = cpfEncryption.Encrypt(dto.Cpf);
                user.CpfHash = cpfHash;
            }
        }

        user.Name = dto.Name;
        user.Email = dto.Email;
        user.RoleId = dto.RoleId;
        user.SchoolId = dto.RoleId == RoleIds.Admin ? null : dto.SchoolId;
        user.StudentId = studentId;
        user.IsActive = dto.IsActive;
        user.Phone = dto.Phone;
        user.UpdatedAt = DateTime.UtcNow;

        unitOfWork.Repository<User>().Update(user);
        if (!dto.IsActive)
            await UserSessions.RevokeAllAsync(context, user.Id, cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user is null)
            return Result<bool>.NotFound("Usuário não encontrado.");

        if (IsDirector && (user.SchoolId != currentUser.SchoolId || !RoleIds.SchoolMembers.Contains(user.RoleId)))
            return Result<bool>.Forbidden("Você não tem permissão para remover este usuário.");

        if (user.Id == currentUser.UserId)
            return Result<bool>.BadRequest("Você não pode remover sua própria conta.");

        // Remoção lógica: preserva o histórico de notas, chamadas e ocorrências
        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        unitOfWork.Repository<User>().Update(user);
        // Conta desativada perde as sessões abertas, inclusive a renovação
        await UserSessions.RevokeAllAsync(context, user.Id, cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.NoContent();
    }

    public Task<Result<bool>> AssignClassAsync(Guid teacherId, Guid classId, CancellationToken cancellationToken = default)
        => AssignToClassAsync(teacherId, classId, "Teacher", "professor",
            () => context.TeacherClasses.AnyAsync(tc => tc.TeacherId == teacherId && tc.ClassId == classId, cancellationToken),
            () => context.TeacherClasses.Add(new TeacherClass { TeacherId = teacherId, ClassId = classId }),
            cancellationToken);

    public async Task<Result<bool>> UnassignClassAsync(Guid teacherId, Guid classId, CancellationToken cancellationToken = default)
    {
        var link = await context.TeacherClasses.Include(x => x.Class)
            .FirstOrDefaultAsync(x => x.TeacherId == teacherId && x.ClassId == classId, cancellationToken);

        if (link is null)
            return Result<bool>.NotFound("Vínculo não encontrado.");

        if (IsDirector && link.Class.SchoolId != currentUser.SchoolId)
            return Result<bool>.Forbidden("Você não tem permissão para alterar este vínculo.");

        context.TeacherClasses.Remove(link);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.NoContent();
    }

    public async Task<Result<bool>> AssignStudentAsync(Guid parentId, Guid studentId, CancellationToken cancellationToken = default)
    {
        var parent = await context.Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == parentId, cancellationToken);

        if (parent is null)
            return Result<bool>.NotFound("Usuário não encontrado.");

        if (parent.Role.Name != "Parent")
            return Result<bool>.BadRequest("Usuário não é um responsável.");

        if (IsDirector && parent.SchoolId != currentUser.SchoolId)
            return Result<bool>.Forbidden("Você não tem permissão para vincular este responsável.");

        var student = await context.Students.Include(s => s.Class)
            .FirstOrDefaultAsync(s => s.Id == studentId, cancellationToken);

        if (student is null)
            return Result<bool>.NotFound("Aluno não encontrado.");

        if (student.Class.SchoolId != parent.SchoolId)
            return Result<bool>.BadRequest("Responsável e aluno precisam ser da mesma escola.");

        var alreadyAssigned = await context.ParentStudents
            .AnyAsync(ps => ps.ParentId == parentId && ps.StudentId == studentId, cancellationToken);

        if (alreadyAssigned)
            return Result<bool>.Conflict("Responsável já está vinculado a este aluno.");

        context.ParentStudents.Add(new ParentStudent { ParentId = parentId, StudentId = studentId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.NoContent();
    }

    public async Task<Result<bool>> UnassignStudentAsync(Guid parentId, Guid studentId, CancellationToken cancellationToken = default)
    {
        var link = await context.ParentStudents
            .Include(x => x.Student).ThenInclude(s => s.Class)
            .FirstOrDefaultAsync(x => x.ParentId == parentId && x.StudentId == studentId, cancellationToken);

        if (link is null)
            return Result<bool>.NotFound("Vínculo não encontrado.");

        if (IsDirector && link.Student.Class.SchoolId != currentUser.SchoolId)
            return Result<bool>.Forbidden("Você não tem permissão para alterar este vínculo.");

        context.ParentStudents.Remove(link);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.NoContent();
    }

    public Task<Result<bool>> AssignOrientadorClassAsync(Guid orientadorId, Guid classId, CancellationToken cancellationToken = default)
        => AssignToClassAsync(orientadorId, classId, "Orientador", "orientador",
            () => context.OrientadorClasses.AnyAsync(oc => oc.OrientadorId == orientadorId && oc.ClassId == classId, cancellationToken),
            () => context.OrientadorClasses.Add(new OrientadorClass { OrientadorId = orientadorId, ClassId = classId }),
            cancellationToken);

    public async Task<Result<bool>> UnassignOrientadorClassAsync(Guid orientadorId, Guid classId, CancellationToken cancellationToken = default)
    {
        var link = await context.OrientadorClasses.Include(x => x.Class)
            .FirstOrDefaultAsync(x => x.OrientadorId == orientadorId && x.ClassId == classId, cancellationToken);

        if (link is null)
            return Result<bool>.NotFound("Vínculo não encontrado.");

        if (IsDirector && link.Class.SchoolId != currentUser.SchoolId)
            return Result<bool>.Forbidden("Você não tem permissão para alterar este vínculo.");

        context.OrientadorClasses.Remove(link);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.NoContent();
    }

    private async Task<Result<bool>> AssignToClassAsync(
        Guid userId, Guid classId, string roleName, string roleLabel,
        Func<Task<bool>> alreadyAssigned, Action addLink, CancellationToken cancellationToken)
    {
        var user = await context.Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
            return Result<bool>.NotFound("Usuário não encontrado.");

        if (user.Role.Name != roleName)
            return Result<bool>.BadRequest($"Usuário não é um {roleLabel}.");

        if (IsDirector && user.SchoolId != currentUser.SchoolId)
            return Result<bool>.Forbidden($"Você não tem permissão para vincular este {roleLabel}.");

        var cls = await context.Classes.FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);

        if (cls is null)
            return Result<bool>.NotFound("Turma não encontrada.");

        if (cls.SchoolId != user.SchoolId)
            return Result<bool>.BadRequest($"A turma não pertence à escola do {roleLabel}.");

        if (await alreadyAssigned())
            return Result<bool>.Conflict($"O {roleLabel} já está vinculado a esta turma.");

        addLink();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.NoContent();
    }

    private async Task<Result<UserListDto>?> ValidateSchoolForRoleAsync(int roleId, Guid? schoolId, CancellationToken cancellationToken)
    {
        if (roleId == RoleIds.Admin)
            return null;

        if (schoolId is null)
            return Result<UserListDto>.BadRequest("Informe a escola para este usuário.");

        var schoolExists = await context.Schools.AnyAsync(s => s.Id == schoolId, cancellationToken);
        return schoolExists ? null : Result<UserListDto>.NotFound("Escola não encontrada.");
    }

    private async Task<Result<UserListDto>?> ValidateStudentLinkAsync(Guid? studentId, Guid? schoolId, Guid? ignoreUserId, CancellationToken cancellationToken)
    {
        if (studentId is null)
            return Result<UserListDto>.BadRequest("Informe o aluno para este usuário.");

        var student = await context.Students.Include(s => s.Class)
            .FirstOrDefaultAsync(s => s.Id == studentId, cancellationToken);

        if (student is null)
            return Result<UserListDto>.NotFound("Aluno não encontrado.");

        if (student.Class.SchoolId != schoolId)
            return Result<UserListDto>.Forbidden("Este aluno não pertence à escola informada.");

        var alreadyHasAccount = await context.Users
            .AnyAsync(u => u.StudentId == studentId && u.Id != ignoreUserId, cancellationToken);

        return alreadyHasAccount ? Result<UserListDto>.Conflict("Este aluno já possui uma conta.") : null;
    }

    private IQueryable<User> ScopedUsers()
    {
        var query = context.Users.AsNoTracking()
            .Include(u => u.Role)
            .Include(u => u.School)
            .Include(u => u.TeacherClasses)
            .Include(u => u.OrientadorClasses)
            .Include(u => u.ParentStudents)
            .AsSplitQuery();

        return currentUser.Role switch
        {
            "Admin" => query,
            "Director" => query.Where(u => u.SchoolId == currentUser.SchoolId),
            _ => query.Where(_ => false)
        };
    }

    private UserListDto ToDto(User u)
    {
        var cpf = u.CpfEncrypted is not null ? cpfEncryption.Decrypt(u.CpfEncrypted) : null;
        var classIds = u.TeacherClasses.Select(tc => tc.ClassId)
            .Concat(u.OrientadorClasses.Select(oc => oc.ClassId))
            .Distinct()
            .ToList();

        return new(u.Id, u.Name, u.Email, u.Role?.Name ?? string.Empty,
            u.SchoolId, u.School?.Name, u.IsActive, u.CreatedAt, cpf, u.Phone,
            u.StudentId, classIds, u.ParentStudents.Select(ps => ps.StudentId).ToList());
    }
}
