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
    // Regras de visibilidade por perfil (quem enxerga o quê) ficam no AccessScope
    private readonly AccessScope scope = new(context, currentUser);

    public async Task<Result<PagedResult<GradeDto>>> GetAllAsync(PagedQuery query, Guid? classId = null, Guid? studentId = null, CancellationToken cancellationToken = default)
    {
        IQueryable<Grade> filtered = scope.Grades().AsNoTracking()
            .Include(g => g.Student)
            .Include(g => g.Class).ThenInclude(c => c.School);

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
        IQueryable<Grade> filtered = scope.Grades().AsNoTracking()
            .Include(g => g.Student)
            .Include(g => g.Class).ThenInclude(c => c.School);

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

        if (currentUser.Role == "Teacher")
        {
            var isTeacherOfClass = await context.TeacherClasses
                .AnyAsync(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == dto.ClassId, cancellationToken);

            if (!isTeacherOfClass)
                return Result<GradeDto>.Forbidden("Você não é professor desta turma.");
        }

        if (currentUser.Role == "Director" && student.Class.SchoolId != currentUser.SchoolId)
            return Result<GradeDto>.Forbidden("Este aluno não pertence à sua escola.");

        if (await HasDuplicateAsync(dto.StudentId, dto.ClassId, dto.Subject, dto.Period, null, cancellationToken))
            return Result<GradeDto>.Conflict("Este aluno já tem nota nesta matéria e período. Edite a nota existente.");

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

        if (await HasDuplicateAsync(grade.StudentId, grade.ClassId, dto.Subject, dto.Period, grade.Id, cancellationToken))
            return Result<GradeDto>.Conflict("Este aluno já tem nota nesta matéria e período.");

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

    // Uma nota por aluno, turma, matéria e período; matéria sem diferenciar maiúsculas
    private async Task<bool> HasDuplicateAsync(Guid studentId, Guid classId, string subject, string period, Guid? ignoreId, CancellationToken cancellationToken)
    {
        var normalizedSubject = subject.Trim().ToLower();
        var candidates = await context.Grades.AsNoTracking()
            .Where(g => g.StudentId == studentId && g.ClassId == classId && g.Id != ignoreId
                        && g.Subject.Trim().ToLower() == normalizedSubject)
            .Select(g => g.Period)
            .ToListAsync(cancellationToken);

        return candidates.Any(p => GradePeriods.AreSame(p, period));
    }

    private static GradeDto ToDto(Grade g) =>
        new(g.Id, g.StudentId, g.Student?.Name ?? string.Empty,
            g.ClassId, g.Class?.Name ?? string.Empty, g.Subject, g.Value, g.Period, g.CreatedAt);
}