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
    public async Task<Result<PagedResult<UserListDto>>> GetAllAsync(PagedQuery query, CancellationToken cancellationToken = default)
    {
        var baseQuery = context.Users.AsNoTracking()
            .Include(u => u.Role)
            .Include(u => u.School);

        var filtered = currentUser.Role switch
        {
            "Admin" => baseQuery,
            "Director" => baseQuery.Where(u => u.SchoolId == currentUser.SchoolId),
            _ => baseQuery.Where(_ => false)
        };

        var totalCount = await filtered.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);
        var data = await filtered.Skip(query.Skip).Take(query.Take).ToListAsync(cancellationToken);

        return Result<PagedResult<UserListDto>>.Success(
            new PagedResult<UserListDto>(data.Select(ToDto), query.Page, query.PageSize, totalCount, totalPages));
    }

    public async Task<Result<UserListDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .Include(u => u.School);

        var filtered = currentUser.Role switch
        {
            "Admin" => query,
            "Director" => query.Where(u => u.SchoolId == currentUser.SchoolId),
            _ => query.Where(_ => false)
        };

        var user = await filtered.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        return user is null
            ? Result<UserListDto>.NotFound("Usuário não encontrado.")
            : Result<UserListDto>.Success(ToDto(user));
    }

    public async Task<Result<UserListDto>> CreateAsync(CreateUserDto dto, CancellationToken cancellationToken = default)
    {
        if (currentUser.Role == "Director")
        {
            if (dto.RoleId is not (3 or 4 or 5 or 6))
                return Result<UserListDto>.Forbidden("Diretor só pode criar Professor, Aluno, Responsável ou Orientador.");

            if (dto.SchoolId != currentUser.SchoolId)
                return Result<UserListDto>.Forbidden("Você só pode criar usuários na sua escola.");
        }

        if (dto.RoleId != 1 && dto.SchoolId is null)
            return Result<UserListDto>.BadRequest("Informe a escola para este usuário.");

        if (dto.SchoolId is not null)
        {
            var schoolExists = await unitOfWork.Repository<School>()
                .ExistsAsync(s => s.Id == dto.SchoolId, cancellationToken);

            if (!schoolExists)
                return Result<UserListDto>.NotFound("Escola não encontrada.");
        }

        var roleExists = await context.Roles.AnyAsync(r => r.Id == dto.RoleId, cancellationToken);
        if (!roleExists)
            return Result<UserListDto>.NotFound("Role não encontrada.");

        var emailExists = await unitOfWork.Repository<User>()
            .ExistsAsync(u => u.Email == dto.Email, cancellationToken);

        if (emailExists)
            return Result<UserListDto>.Conflict("E-mail já cadastrado.");

        // Validações específicas para role Student
        if (dto.RoleId == 4)
        {
            if (dto.StudentId is null)
                return Result<UserListDto>.BadRequest("Informe o aluno para este usuário.");

            var student = await context.Students
                .Include(s => s.Class)
                .FirstOrDefaultAsync(s => s.Id == dto.StudentId, cancellationToken);

            if (student is null)
                return Result<UserListDto>.NotFound("Aluno não encontrado.");

            if (currentUser.Role == "Director" && student.Class.SchoolId != currentUser.SchoolId)
                return Result<UserListDto>.Forbidden("Este aluno não pertence à sua escola.");

            var alreadyHasAccount = await context.Users
                .AnyAsync(u => u.StudentId == dto.StudentId, cancellationToken);

            if (alreadyHasAccount)
                return Result<UserListDto>.Conflict("Este aluno já possui uma conta.");
        }

        var user = new User
        {
            Name = dto.Name,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            RoleId = dto.RoleId,
            SchoolId = dto.SchoolId,
            StudentId = dto.StudentId,
            CpfEncrypted = dto.Cpf is null ? null : cpfEncryption.Encrypt(dto.Cpf),
            CpfHash = dto.Cpf is null ? null : cpfEncryption.Hash(dto.Cpf),
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

        // Director não pode editar usuários de outra escola
        if (currentUser.Role == "Director" && user.SchoolId != currentUser.SchoolId)
            return Result<UserListDto>.Forbidden("Você não tem permissão para editar este usuário.");

        // Somente Admin pode promover/rebaixar para Admin
        if (dto.RoleId == 1 && currentUser.Role != "Admin")
            return Result<UserListDto>.Forbidden("Apenas administradores podem definir a role Admin.");

        var roleExists = await context.Roles.AnyAsync(r => r.Id == dto.RoleId, cancellationToken);
        if (!roleExists)
            return Result<UserListDto>.NotFound("Role não encontrada.");

        user.Name = dto.Name;
        user.Email = dto.Email;
        user.RoleId = dto.RoleId;
        user.SchoolId = dto.SchoolId;
        user.IsActive = dto.IsActive;
        user.CpfEncrypted = dto.Cpf is null ? null : cpfEncryption.Encrypt(dto.Cpf);
        user.CpfHash = dto.Cpf is null ? null : cpfEncryption.Hash(dto.Cpf);
        user.Phone = dto.Phone;

        unitOfWork.Repository<User>().Update(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
            return Result<bool>.NotFound("Usuário não encontrado.");

        if (currentUser.Role == "Director" && user.SchoolId != currentUser.SchoolId)
            return Result<bool>.Forbidden("Você não tem permissão para remover este usuário.");

        // Não permite deletar o próprio usuário
        if (user.Id == currentUser.UserId)
            return Result<bool>.BadRequest("Você não pode remover sua própria conta.");

        user.IsActive = false;
        unitOfWork.Repository<User>().Update(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.NoContent();
    }

    public async Task<Result<bool>> AssignClassAsync(Guid teacherId, Guid classId, CancellationToken cancellationToken = default)
    {
        var teacher = await context.Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == teacherId, cancellationToken);

        if (teacher is null)
            return Result<bool>.NotFound("Usuário não encontrado.");

        if (teacher.Role.Name != "Teacher")
            return Result<bool>.BadRequest("Usuário não é um professor.");

        if (currentUser.Role == "Director" && teacher.SchoolId != currentUser.SchoolId)
            return Result<bool>.Forbidden("Você não tem permissão para vincular este professor.");

        var cls = await context.Classes.FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);
        if (cls is null)
            return Result<bool>.NotFound("Turma não encontrada.");

        if (currentUser.Role == "Director" && cls.SchoolId != currentUser.SchoolId)
            return Result<bool>.Forbidden("Você não tem permissão para vincular a esta turma.");

        var alreadyAssigned = await context.TeacherClasses
            .AnyAsync(tc => tc.TeacherId == teacherId && tc.ClassId == classId, cancellationToken);

        if (alreadyAssigned)
            return Result<bool>.Conflict("Professor já está vinculado a esta turma.");

        context.TeacherClasses.Add(new TeacherClass { TeacherId = teacherId, ClassId = classId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.NoContent();
    }

    public async Task<Result<bool>> UnassignClassAsync(Guid teacherId, Guid classId, CancellationToken cancellationToken = default)
    {
        var tc = await context.TeacherClasses
            .FirstOrDefaultAsync(x => x.TeacherId == teacherId && x.ClassId == classId, cancellationToken);

        if (tc is null)
            return Result<bool>.NotFound("Vínculo não encontrado.");

        context.TeacherClasses.Remove(tc);
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

        var studentExists = await unitOfWork.Repository<Student>()
            .ExistsAsync(s => s.Id == studentId, cancellationToken);

        if (!studentExists)
            return Result<bool>.NotFound("Aluno não encontrado.");

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
        var ps = await context.ParentStudents
            .FirstOrDefaultAsync(x => x.ParentId == parentId && x.StudentId == studentId, cancellationToken);

        if (ps is null)
            return Result<bool>.NotFound("Vínculo não encontrado.");

        context.ParentStudents.Remove(ps);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.NoContent();
    }

    public async Task<Result<bool>> AssignOrientadorClassAsync(Guid orientadorId, Guid classId, CancellationToken cancellationToken = default)
    {
        var orientador = await context.Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == orientadorId, cancellationToken);

        if (orientador is null)
            return Result<bool>.NotFound("Usuário não encontrado.");

        if (orientador.Role.Name != "Orientador")
            return Result<bool>.BadRequest("Usuário não é um orientador.");

        if (currentUser.Role == "Director" && orientador.SchoolId != currentUser.SchoolId)
            return Result<bool>.Forbidden("Você não tem permissão para vincular este orientador.");

        var cls = await context.Classes.FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);
        if (cls is null)
            return Result<bool>.NotFound("Turma não encontrada.");

        if (currentUser.Role == "Director" && cls.SchoolId != currentUser.SchoolId)
            return Result<bool>.Forbidden("Você não tem permissão para vincular a esta turma.");

        var alreadyAssigned = await context.OrientadorClasses
            .AnyAsync(oc => oc.OrientadorId == orientadorId && oc.ClassId == classId, cancellationToken);

        if (alreadyAssigned)
            return Result<bool>.Conflict("Orientador já está vinculado a esta turma.");

        context.OrientadorClasses.Add(new OrientadorClass { OrientadorId = orientadorId, ClassId = classId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.NoContent();
    }

    public async Task<Result<bool>> UnassignOrientadorClassAsync(Guid orientadorId, Guid classId, CancellationToken cancellationToken = default)
    {
        var oc = await context.OrientadorClasses
            .FirstOrDefaultAsync(x => x.OrientadorId == orientadorId && x.ClassId == classId, cancellationToken);

        if (oc is null)
            return Result<bool>.NotFound("Vínculo não encontrado.");

        context.OrientadorClasses.Remove(oc);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.NoContent();
    }

    private UserListDto ToDto(User u)
    {
        var cpf = u.CpfEncrypted is not null ? cpfEncryption.Decrypt(u.CpfEncrypted) : null;
        return new(u.Id, u.Name, u.Email, u.Role?.Name ?? string.Empty,
            u.SchoolId, u.School?.Name, u.IsActive, u.CreatedAt, cpf, u.Phone);
    }
}