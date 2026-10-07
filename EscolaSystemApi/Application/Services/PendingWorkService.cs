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
    // Regras de visibilidade por perfil (quem enxerga o quê) ficam no AccessScope
    private readonly AccessScope scope = new(context, currentUser);

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

    public async Task<Result<ClassAssignmentDto>> CreateForClassAsync(CreateClassAssignmentDto dto, CancellationToken cancellationToken = default)
    {
        var cls = await context.Classes.FirstOrDefaultAsync(c => c.Id == dto.ClassId, cancellationToken);
        if (cls is null)
            return Result<ClassAssignmentDto>.NotFound("Turma não encontrada.");

        if (!await CanManageClassAsync(cls, cancellationToken))
            return Result<ClassAssignmentDto>.Forbidden("Você não pode lançar trabalhos nesta turma.");

        var studentIds = await context.Students
            .Where(s => s.ClassId == cls.Id && s.IsActive)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        if (studentIds.Count == 0)
            return Result<ClassAssignmentDto>.BadRequest("A turma não tem alunos ativos.");

        // Um registro por aluno, todos com o mesmo AssignmentId, gravados juntos: ou a turma toda recebe, ou ninguém
        var assignmentId = Guid.NewGuid();
        context.PendingWorks.AddRange(studentIds.Select(studentId => new PendingWork
        {
            AssignmentId = assignmentId,
            StudentId = studentId,
            ClassId = cls.Id,
            Title = dto.Title.Trim(),
            Description = dto.Description.Trim(),
            DueDate = dto.DueDate
        }));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<ClassAssignmentDto>.Created(new ClassAssignmentDto(
            assignmentId, cls.Id, cls.Name, dto.Title.Trim(), dto.Description.Trim(), dto.DueDate, studentIds.Count, 0));
    }

    public async Task<Result<ClassAssignmentDto>> UpdateAssignmentAsync(Guid assignmentId, UpdateAssignmentDto dto, CancellationToken cancellationToken = default)
    {
        var works = await context.PendingWorks.Include(p => p.Class)
            .Where(p => p.AssignmentId == assignmentId)
            .ToListAsync(cancellationToken);

        if (works.Count == 0)
            return Result<ClassAssignmentDto>.NotFound("Trabalho não encontrado.");

        var cls = works[0].Class;
        if (!await CanManageClassAsync(cls, cancellationToken))
            return Result<ClassAssignmentDto>.Forbidden("Você não pode alterar trabalhos desta turma.");

        foreach (var work in works)
        {
            work.Title = dto.Title.Trim();
            work.Description = dto.Description.Trim();
            work.DueDate = dto.DueDate;
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<ClassAssignmentDto>.Success(new ClassAssignmentDto(
            assignmentId, cls.Id, cls.Name, dto.Title.Trim(), dto.Description.Trim(), dto.DueDate,
            works.Count, works.Count(w => w.IsDelivered)));
    }

    public async Task<Result<bool>> DeleteAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken = default)
    {
        var works = await context.PendingWorks.Include(p => p.Class)
            .Where(p => p.AssignmentId == assignmentId)
            .ToListAsync(cancellationToken);

        if (works.Count == 0)
            return Result<bool>.NotFound("Trabalho não encontrado.");

        if (!await CanManageClassAsync(works[0].Class, cancellationToken))
            return Result<bool>.Forbidden("Você não pode excluir trabalhos desta turma.");

        context.PendingWorks.RemoveRange(works);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.NoContent();
    }

    // Quem lança trabalho na turma pode corrigi-lo ou excluí-lo: o professor dela e a direção da escola
    private async Task<bool> CanManageClassAsync(Class cls, CancellationToken cancellationToken) => currentUser.Role switch
    {
        "Director" => cls.SchoolId == currentUser.SchoolId,
        "Teacher" => await context.TeacherClasses
            .AnyAsync(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == cls.Id, cancellationToken),
        _ => false
    };

    private IQueryable<PendingWork> ScopedWorks() =>
        scope.PendingWorks().AsNoTracking().Include(p => p.Student).Include(p => p.Class);

    private static PendingWorkDto ToDto(PendingWork p) =>
        new(p.Id, p.StudentId, p.Student?.Name ?? string.Empty, p.ClassId, p.Class?.Name ?? string.Empty,
            p.Title, p.Description, p.DueDate, p.IsDelivered, p.DeliveredAt, p.CreatedAt, p.AssignmentId);
}
