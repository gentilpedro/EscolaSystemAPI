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
    public async Task<Result<PagedResult<PendingWorkDto>>> GetAllAsync(PagedQuery query, CancellationToken cancellationToken = default)
    {
        var baseQuery = context.PendingWorks.AsNoTracking()
            .Include(p => p.Student)
            .Include(p => p.Class).ThenInclude(c => c.School);

        var filtered = currentUser.Role switch
        {
            "Admin" => baseQuery,
            "Director" => baseQuery.Where(p => p.Class.SchoolId == currentUser.SchoolId),
            "Teacher" => baseQuery.Where(p => context.TeacherClasses
                .Any(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == p.ClassId)),
            "Student" => baseQuery.Where(p => p.StudentId == currentUser.StudentId),
            "Parent" => baseQuery.Where(p => context.ParentStudents
                .Any(ps => ps.ParentId == currentUser.UserId && ps.StudentId == p.StudentId)),
            _ => baseQuery.Where(_ => false)
        };

        var totalCount = await filtered.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);
        var data = await filtered.Skip(query.Skip).Take(query.Take).ToListAsync(cancellationToken);

        return Result<PagedResult<PendingWorkDto>>.Success(
            new PagedResult<PendingWorkDto>(data.Select(ToDto), query.Page, query.PageSize, totalCount, totalPages));
    }

    public async Task<Result<PendingWorkDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = context.PendingWorks.AsNoTracking()
            .Include(p => p.Student)
            .Include(p => p.Class).ThenInclude(c => c.School);

        var filtered = currentUser.Role switch
        {
            "Admin" => query,
            "Director" => query.Where(p => p.Class.SchoolId == currentUser.SchoolId),
            "Teacher" => query.Where(p => context.TeacherClasses
                .Any(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == p.ClassId)),
            "Student" => query.Where(p => p.StudentId == currentUser.StudentId),
            "Parent" => query.Where(p => context.ParentStudents
                .Any(ps => ps.ParentId == currentUser.UserId && ps.StudentId == p.StudentId)),
            _ => query.Where(_ => false)
        };

        var work = await filtered.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
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

        var classExists = await unitOfWork.Repository<Class>()
            .ExistsAsync(c => c.Id == dto.ClassId, cancellationToken);

        if (!classExists)
            return Result<PendingWorkDto>.NotFound("Turma não encontrada.");

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
        var work = await unitOfWork.Repository<PendingWork>().GetByIdAsync(id, cancellationToken);
        if (work is null)
            return Result<PendingWorkDto>.NotFound("Trabalho pendente não encontrado.");

        if (work.IsDelivered)
            return Result<PendingWorkDto>.BadRequest("Trabalho já foi entregue.");

        work.IsDelivered = true;
        work.DeliveredAt = DateTime.UtcNow;

        unitOfWork.Repository<PendingWork>().Update(work);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    private static PendingWorkDto ToDto(PendingWork p) =>
        new(p.Id, p.StudentId, p.Student?.Name ?? string.Empty, p.ClassId, p.Class?.Name ?? string.Empty,
            p.Title, p.Description, p.DueDate, p.IsDelivered, p.DeliveredAt, p.CreatedAt);
}