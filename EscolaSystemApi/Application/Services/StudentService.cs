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
    public async Task<Result<PagedResult<StudentDto>>> GetAllAsync(PagedQuery query, Guid? classId = null, CancellationToken cancellationToken = default)
    {
        var baseQuery = context.Students.AsNoTracking().Include(s => s.Class).ThenInclude(c => c.School);

        var filtered = currentUser.Role switch
        {
            "Admin" => baseQuery,
            "Director" => baseQuery.Where(s => s.Class.SchoolId == currentUser.SchoolId),
            "Teacher" => baseQuery.Where(s => context.TeacherClasses
                .Any(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == s.ClassId)),
            "Orientador" => baseQuery.Where(s => context.OrientadorClasses
                .Any(oc => oc.OrientadorId == currentUser.UserId && oc.ClassId == s.ClassId)),
            "Student" => baseQuery.Where(s => s.Id == currentUser.StudentId),
            "Parent" => baseQuery.Where(s => context.ParentStudents
                .Any(ps => ps.ParentId == currentUser.UserId && ps.StudentId == s.Id)),
            _ => baseQuery.Where(_ => false)
        };

        if (classId.HasValue)
            filtered = filtered.Where(s => s.ClassId == classId.Value);

        var totalCount = await filtered.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);
        var data = await filtered.Skip(query.Skip).Take(query.Take).ToListAsync(cancellationToken);

        return Result<PagedResult<StudentDto>>.Success(
            new PagedResult<StudentDto>(data.Select(ToDto), query.Page, query.PageSize, totalCount, totalPages));
    }

    public async Task<Result<StudentDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = context.Students.AsNoTracking().Include(s => s.Class).ThenInclude(c => c.School);

        var filtered = currentUser.Role switch
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

        var student = await filtered.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
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

        var classExists = await unitOfWork.Repository<Class>()
            .ExistsAsync(c => c.Id == dto.ClassId, cancellationToken);

        if (!classExists)
            return Result<StudentDto>.NotFound("Turma não encontrada.");

        student.Name = dto.Name;
        student.Email = dto.Email;
        student.Registration = dto.Registration;
        student.BirthDate = dto.BirthDate;
        student.ClassId = dto.ClassId;
        student.IsActive = dto.IsActive;

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

        unitOfWork.Repository<Student>().Remove(student);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.NoContent();
    }

    private static StudentDto ToDto(Student s) =>
        new(s.Id, s.Name, s.Email, s.Registration, s.BirthDate, s.ClassId,
            s.Class?.Name ?? string.Empty, s.IsActive, s.CreatedAt);
}