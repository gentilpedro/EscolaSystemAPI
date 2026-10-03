using EscolaSystemApi.Application.DTOs.PendingWorks;
using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Application.Interfaces.Repositories;
using EscolaSystemApi.Common;
using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscolaSystemApi.Application.Services;

public class PendingWorkService(IUnitOfWork unitOfWork, AppDbContext context, ICurrentUserService currentUser) : IPendingWorkService
{
    public async Task<Result<PagedResult<PendingWorkDto>>> GetAllAsync(PagedQuery query, Guid? classId = null, Guid? studentId = null, CancellationToken cancellationToken = default)
    {
        var filtered = ScopedWorks();

        if (classId.HasValue)
            filtered = filtered.Where(p => p.ClassId == classId.Value);
        if (studentId.HasValue)
            filtered = filtered.Where(p => p.StudentId == studentId.Value);

        var totalCount = await filtered.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);
        var data = await filtered
            .OrderBy(p => p.IsDelivered).ThenBy(p => p.DueDate)
            .Skip(query.Skip).Take(query.Take)
            .ToListAsync(cancellationToken);

        return Result<PagedResult<PendingWorkDto>>.Success(
            new PagedResult<PendingWorkDto>(data.Select(ToDto), query.Page, query.PageSize, totalCount, totalPages));
    }

    public async Task<Result<PendingWorkDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var work = await ScopedWorks().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return work is null
            ? Result<PendingWorkDto>.NotFound("Trabalho pendente não encontrado.")
            : Result<PendingWorkDto>.Success(ToDto(work));
    }

    public async Task<Result<PendingWorkDto>> CreateAsync(CreatePendingWorkDto dto, CancellationToken cancellationToken = default)
    {
        var student = await context.Students.Include(s => s.Class)
            .FirstOrDefaultAsync(s => s.Id == dto.StudentId, cancellationToken);

        if (student is null)
            return Result<PendingWorkDto>.NotFound("Aluno não encontrado.");

        // Validar se o aluno pertence à turma informada
        if (student.ClassId != dto.ClassId)
            return Result<PendingWorkDto>.BadRequest("Aluno não pertence a esta turma.");

        if (currentUser.Role == "Teacher")
        {
            var isTeacherOfClass = await context.TeacherClasses
                .AnyAsync(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == dto.ClassId, cancellationToken);

            if (!isTeacherOfClass)
                return Result<PendingWorkDto>.Forbidden("Você não é professor desta turma.");
        }

        if (currentUser.Role == "Director" && student.Class.SchoolId != currentUser.SchoolId)
            return Result<PendingWorkDto>.Forbidden("Este aluno não pertence à sua escola.");

        var work = new PendingWork
        {
            StudentId = dto.StudentId,
            ClassId = dto.ClassId,
            Title = dto.Title,
            Description = dto.Description,
            DueDate = dto.DueDate
        };

        await unitOfWork.Repository<PendingWork>().AddAsync(work, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(work.Id, cancellationToken) is { IsSuccess: true } r
            ? Result<PendingWorkDto>.Created(r.Data!)
            : Result<PendingWorkDto>.BadRequest("Erro ao criar trabalho.");
    }

    public async Task<Result<PendingWorkDto>> MarkAsDeliveredAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Busca pelo escopo do usuário: aluno só enxerga (e entrega) os próprios trabalhos
        var visible = await ScopedWorks().AnyAsync(p => p.Id == id, cancellationToken);
        if (!visible)
            return Result<PendingWorkDto>.NotFound("Trabalho pendente não encontrado.");

        var work = await context.PendingWorks.FirstAsync(p => p.Id == id, cancellationToken);

        if (work.IsDelivered)
            return Result<PendingWorkDto>.BadRequest("Trabalho já foi entregue.");

        work.IsDelivered = true;
        work.DeliveredAt = DateTime.UtcNow;
        work.UpdatedAt = DateTime.UtcNow;

        unitOfWork.Repository<PendingWork>().Update(work);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    private IQueryable<PendingWork> ScopedWorks()
    {
        var query = context.PendingWorks.AsNoTracking()
            .Include(p => p.Student)
            .Include(p => p.Class);

        return currentUser.Role switch
        {
            "Admin" => query,
            "Director" => query.Where(p => p.Class.SchoolId == currentUser.SchoolId),
            "Teacher" => query.Where(p => context.TeacherClasses
                .Any(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == p.ClassId)),
            "Orientador" => query.Where(p => context.OrientadorClasses
                .Any(oc => oc.OrientadorId == currentUser.UserId && oc.ClassId == p.ClassId)),
            "Student" => query.Where(p => p.StudentId == currentUser.StudentId),
            "Parent" => query.Where(p => context.ParentStudents
                .Any(ps => ps.ParentId == currentUser.UserId && ps.StudentId == p.StudentId)),
            _ => query.Where(_ => false)
        };
    }

    private static PendingWorkDto ToDto(PendingWork p) =>
        new(p.Id, p.StudentId, p.Student?.Name ?? string.Empty, p.ClassId, p.Class?.Name ?? string.Empty,
            p.Title, p.Description, p.DueDate, p.IsDelivered, p.DeliveredAt, p.CreatedAt);
}
