using EscolaSystemApi.Application.DTOs.Grades;
using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Application.Interfaces.Repositories;
using EscolaSystemApi.Common;
using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscolaSystemApi.Application.Services;

public class GradeService(IUnitOfWork unitOfWork, AppDbContext context, ICurrentUserService currentUser) : IGradeService
{
    public async Task<Result<PagedResult<GradeDto>>> GetAllAsync(PagedQuery query, Guid? classId = null, Guid? studentId = null, CancellationToken cancellationToken = default)
    {
        var baseQuery = context.Grades.AsNoTracking()
            .Include(g => g.Student)
            .Include(g => g.Class).ThenInclude(c => c.School);

        var filtered = currentUser.Role switch
        {
            "Admin" => baseQuery,
            "Director" => baseQuery.Where(g => g.Class.SchoolId == currentUser.SchoolId),
            "Teacher" => baseQuery.Where(g => context.TeacherClasses
                .Any(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == g.ClassId)),
            "Orientador" => baseQuery.Where(g => context.OrientadorClasses
                .Any(oc => oc.OrientadorId == currentUser.UserId && oc.ClassId == g.ClassId)),
            "Student" => baseQuery.Where(g => g.StudentId == currentUser.StudentId),
            "Parent" => baseQuery.Where(g => context.ParentStudents
                .Any(ps => ps.ParentId == currentUser.UserId && ps.StudentId == g.StudentId)),
            _ => baseQuery.Where(_ => false)
        };

        if (classId.HasValue)
            filtered = filtered.Where(g => g.ClassId == classId.Value);
        if (studentId.HasValue)
            filtered = filtered.Where(g => g.StudentId == studentId.Value);

        var totalCount = await filtered.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);
        var data = await filtered.OrderBy(g => g.Student.Name).ThenBy(g => g.Subject).Skip(query.Skip).Take(query.Take).ToListAsync(cancellationToken);

        return Result<PagedResult<GradeDto>>.Success(
            new PagedResult<GradeDto>(data.Select(ToDto), query.Page, query.PageSize, totalCount, totalPages));
    }

    public async Task<Result<GradeDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = context.Grades.AsNoTracking()
            .Include(g => g.Student)
            .Include(g => g.Class).ThenInclude(c => c.School);

        var filtered = currentUser.Role switch
        {
            "Admin" => query,
            "Director" => query.Where(g => g.Class.SchoolId == currentUser.SchoolId),
            "Teacher" => query.Where(g => context.TeacherClasses
                .Any(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == g.ClassId)),
            "Orientador" => query.Where(g => context.OrientadorClasses
                .Any(oc => oc.OrientadorId == currentUser.UserId && oc.ClassId == g.ClassId)),
            "Student" => query.Where(g => g.StudentId == currentUser.StudentId),
            "Parent" => query.Where(g => context.ParentStudents
                .Any(ps => ps.ParentId == currentUser.UserId && ps.StudentId == g.StudentId)),
            _ => query.Where(_ => false)
        };

        var grade = await filtered.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        return grade is null
            ? Result<GradeDto>.NotFound("Nota não encontrada.")
            : Result<GradeDto>.Success(ToDto(grade));
    }

    public async Task<Result<GradeDto>> CreateAsync(CreateGradeDto dto, CancellationToken cancellationToken = default)
    {
        var student = await context.Students.Include(s => s.Class)
            .FirstOrDefaultAsync(s => s.Id == dto.StudentId, cancellationToken);

        if (student is null)
            return Result<GradeDto>.NotFound("Aluno não encontrado.");

        // Validar se o aluno pertence à turma informada
        if (student.ClassId != dto.ClassId)
            return Result<GradeDto>.BadRequest("Aluno não pertence a esta turma.");

        var classExists = await unitOfWork.Repository<Class>()
            .ExistsAsync(c => c.Id == dto.ClassId, cancellationToken);

        if (!classExists)
            return Result<GradeDto>.NotFound("Turma não encontrada.");

        if (currentUser.Role == "Teacher")
        {
            var isTeacherOfClass = await context.TeacherClasses
                .AnyAsync(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == dto.ClassId, cancellationToken);

            if (!isTeacherOfClass)
                return Result<GradeDto>.Forbidden("Você não é professor desta turma.");
        }

        if (currentUser.Role == "Director" && student.Class.SchoolId != currentUser.SchoolId)
            return Result<GradeDto>.Forbidden("Este aluno não pertence à sua escola.");

        var grade = new Grade
        {
            StudentId = dto.StudentId,
            ClassId = dto.ClassId,
            Subject = dto.Subject,
            Value = dto.Value,
            Period = dto.Period
        };

        await unitOfWork.Repository<Grade>().AddAsync(grade, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(grade.Id, cancellationToken) is { IsSuccess: true } r
            ? Result<GradeDto>.Created(r.Data!)
            : Result<GradeDto>.BadRequest("Erro ao criar nota.");
    }

    public async Task<Result<GradeDto>> UpdateAsync(Guid id, UpdateGradeDto dto, CancellationToken cancellationToken = default)
    {
        var grade = await context.Grades.Include(g => g.Class)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

        if (grade is null)
            return Result<GradeDto>.NotFound("Nota não encontrada.");

        if (currentUser.Role == "Teacher")
        {
            var isTeacherOfClass = await context.TeacherClasses
                .AnyAsync(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == grade.ClassId, cancellationToken);

            if (!isTeacherOfClass)
                return Result<GradeDto>.Forbidden("Você não é professor desta turma.");
        }

        if (currentUser.Role == "Director" && grade.Class.SchoolId != currentUser.SchoolId)
            return Result<GradeDto>.Forbidden("Esta nota não pertence à sua escola.");

        grade.Subject = dto.Subject;
        grade.Value = dto.Value;
        grade.Period = dto.Period;
        grade.UpdatedAt = DateTime.UtcNow;

        unitOfWork.Repository<Grade>().Update(grade);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var grade = await context.Grades.Include(g => g.Class)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

        if (grade is null)
            return Result<bool>.NotFound("Nota não encontrada.");

        if (currentUser.Role == "Teacher")
        {
            var isTeacherOfClass = await context.TeacherClasses
                .AnyAsync(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == grade.ClassId, cancellationToken);

            if (!isTeacherOfClass)
                return Result<bool>.Forbidden("Você não é professor desta turma.");
        }

        if (currentUser.Role == "Director" && grade.Class.SchoolId != currentUser.SchoolId)
            return Result<bool>.Forbidden("Esta nota não pertence à sua escola.");

        unitOfWork.Repository<Grade>().Remove(grade);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }

    private static GradeDto ToDto(Grade g) =>
        new(g.Id, g.StudentId, g.Student?.Name ?? string.Empty,
            g.ClassId, g.Class?.Name ?? string.Empty, g.Subject, g.Value, g.Period, g.CreatedAt);
}