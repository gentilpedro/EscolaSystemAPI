using EscolaSystemApi.Application.DTOs.Students;
using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Application.Interfaces.Repositories;
using EscolaSystemApi.Common;
using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscolaSystemApi.Application.Services;

public class StudentService(IUnitOfWork unitOfWork, AppDbContext context, ICurrentUserService currentUser) : IStudentService
{
    public async Task<Result<PagedResult<StudentDto>>> GetAllAsync(PagedQuery query, Guid? classId = null, Guid? schoolId = null, bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var filtered = ScopedStudents();

        if (classId.HasValue)
            filtered = filtered.Where(s => s.ClassId == classId.Value);
        if (schoolId.HasValue)
            filtered = filtered.Where(s => s.Class.SchoolId == schoolId.Value);
        if (isActive.HasValue)
            filtered = filtered.Where(s => s.IsActive == isActive.Value);

        var totalCount = await filtered.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);
        var data = await filtered
            .OrderBy(s => s.Name)
            .Skip(query.Skip).Take(query.Take)
            .ToListAsync(cancellationToken);

        return Result<PagedResult<StudentDto>>.Success(
            new PagedResult<StudentDto>(data.Select(ToDto), query.Page, query.PageSize, totalCount, totalPages));
    }

    public async Task<Result<StudentDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var student = await ScopedStudents().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        return student is null
            ? Result<StudentDto>.NotFound("Aluno não encontrado.")
            : Result<StudentDto>.Success(ToDto(student));
    }

    public async Task<Result<StudentDto>> CreateAsync(CreateStudentDto dto, CancellationToken cancellationToken = default)
    {
        var cls = await context.Classes.FirstOrDefaultAsync(c => c.Id == dto.ClassId, cancellationToken);

        if (cls is null)
            return Result<StudentDto>.NotFound("Turma não encontrada.");

        if (currentUser.Role == "Director" && cls.SchoolId != currentUser.SchoolId)
            return Result<StudentDto>.Forbidden("Você não tem permissão para adicionar alunos nesta turma.");

        if (!cls.IsActive)
            return Result<StudentDto>.BadRequest("Não é possível matricular alunos em uma turma inativa.");

        var regExists = await unitOfWork.Repository<Student>()
            .ExistsAsync(s => s.Registration == dto.Registration, cancellationToken);

        if (regExists)
            return Result<StudentDto>.Conflict("Matrícula já cadastrada.");

        var student = new Student
        {
            Name = dto.Name,
            Email = dto.Email,
            Registration = dto.Registration,
            BirthDate = dto.BirthDate,
            ClassId = dto.ClassId
        };

        await unitOfWork.Repository<Student>().AddAsync(student, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(student.Id, cancellationToken) is { IsSuccess: true } r
            ? Result<StudentDto>.Created(r.Data!)
            : Result<StudentDto>.BadRequest("Erro ao criar aluno.");
    }

    public async Task<Result<StudentDto>> UpdateAsync(Guid id, UpdateStudentDto dto, CancellationToken cancellationToken = default)
    {
        var student = await context.Students.Include(s => s.Class)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (student is null)
            return Result<StudentDto>.NotFound("Aluno não encontrado.");

        if (currentUser.Role == "Director" && student.Class.SchoolId != currentUser.SchoolId)
            return Result<StudentDto>.Forbidden("Você não tem permissão para editar este aluno.");

        var targetClass = await context.Classes.FirstOrDefaultAsync(c => c.Id == dto.ClassId, cancellationToken);

        if (targetClass is null)
            return Result<StudentDto>.NotFound("Turma não encontrada.");

        // Transferência só dentro da mesma escola: responsáveis e conta do aluno pertencem a ela
        if (targetClass.SchoolId != student.Class.SchoolId)
            return Result<StudentDto>.BadRequest("A nova turma precisa ser da mesma escola do aluno.");

        if (targetClass.Id != student.ClassId && !targetClass.IsActive)
            return Result<StudentDto>.BadRequest("Não é possível transferir o aluno para uma turma inativa.");

        var regExists = await context.Students
            .AnyAsync(s => s.Registration == dto.Registration && s.Id != id, cancellationToken);

        if (regExists)
            return Result<StudentDto>.Conflict("Matrícula já cadastrada.");

        student.Name = dto.Name;
        student.Email = dto.Email;
        student.Registration = dto.Registration;
        student.BirthDate = dto.BirthDate;
        student.ClassId = dto.ClassId;
        student.IsActive = dto.IsActive;
        student.UpdatedAt = DateTime.UtcNow;

        unitOfWork.Repository<Student>().Update(student);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var student = await context.Students.Include(s => s.Class)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (student is null)
            return Result<bool>.NotFound("Aluno não encontrado.");

        if (currentUser.Role == "Director" && student.Class.SchoolId != currentUser.SchoolId)
            return Result<bool>.Forbidden("Você não tem permissão para remover este aluno.");

        // A exclusão apagaria em cascata o histórico escolar do aluno
        var hasHistory = await context.Grades.AnyAsync(g => g.StudentId == id, cancellationToken)
                         || await context.Attendances.AnyAsync(a => a.StudentId == id, cancellationToken)
                         || await context.DisciplinaryCalls.AnyAsync(d => d.StudentId == id, cancellationToken)
                         || await context.PendingWorks.AnyAsync(p => p.StudentId == id, cancellationToken);

        if (hasHistory)
            return Result<bool>.Conflict("O aluno possui histórico escolar. Desative-o em vez de excluir.");

        unitOfWork.Repository<Student>().Remove(student);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.NoContent();
    }

    private IQueryable<Student> ScopedStudents()
    {
        var query = context.Students.AsNoTracking().Include(s => s.Class).ThenInclude(c => c.School);

        return currentUser.Role switch
        {
            "Admin" => query,
            "Director" => query.Where(s => s.Class.SchoolId == currentUser.SchoolId),
            "Teacher" => query.Where(s => context.TeacherClasses
                .Any(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == s.ClassId)),
            "Orientador" => query.Where(s => context.OrientadorClasses
                .Any(oc => oc.OrientadorId == currentUser.UserId && oc.ClassId == s.ClassId)),
            "Student" => query.Where(s => s.Id == currentUser.StudentId),
            "Parent" => query.Where(s => context.ParentStudents
                .Any(ps => ps.ParentId == currentUser.UserId && ps.StudentId == s.Id)),
            _ => query.Where(_ => false)
        };
    }

    private static StudentDto ToDto(Student s) =>
        new(s.Id, s.Name, s.Email, s.Registration, s.BirthDate, s.ClassId,
            s.Class?.Name ?? string.Empty, s.IsActive, s.CreatedAt);
}
