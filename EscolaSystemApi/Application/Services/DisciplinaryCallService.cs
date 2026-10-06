using EscolaSystemApi.Application.DTOs.DisciplinaryCalls;
using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Application.Interfaces.Repositories;
using EscolaSystemApi.Common;
using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Domain.Enums;
using EscolaSystemApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscolaSystemApi.Application.Services;

public class DisciplinaryCallService(IUnitOfWork unitOfWork, AppDbContext context, ICurrentUserService currentUser) : IDisciplinaryCallService
{
    // Regras de visibilidade por perfil (quem enxerga o quê) ficam no AccessScope
    private readonly AccessScope scope = new(context, currentUser);

    public async Task<Result<PagedResult<DisciplinaryCallDto>>> GetAllAsync(PagedQuery query, DisciplinaryCallFilter? filter = null, CancellationToken cancellationToken = default)
    {
        var filtered = ScopedCalls();

        if (filter?.SchoolId is { } schoolId)
            filtered = filtered.Where(d => d.Student.Class.SchoolId == schoolId);
        if (filter?.StudentId is { } studentId)
            filtered = filtered.Where(d => d.StudentId == studentId);
        if (filter?.ClassId is { } classId)
            filtered = filtered.Where(d => d.Student.ClassId == classId);
        if (filter?.Status is { } status)
            filtered = filtered.Where(d => d.Status == status);

        var totalCount = await filtered.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);
        var data = await filtered
            .OrderByDescending(d => d.CreatedAt)
            .Skip(query.Skip).Take(query.Take)
            .ToListAsync(cancellationToken);

        return Result<PagedResult<DisciplinaryCallDto>>.Success(
            new PagedResult<DisciplinaryCallDto>(data.Select(ToDto), query.Page, query.PageSize, totalCount, totalPages));
    }

    public async Task<Result<DisciplinaryCallDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var call = await ScopedCalls().FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        return call is null
            ? Result<DisciplinaryCallDto>.NotFound("Chamado disciplinar não encontrado.")
            : Result<DisciplinaryCallDto>.Success(ToDto(call));
    }

    public async Task<Result<DisciplinaryCallDto>> CreateAsync(CreateDisciplinaryCallDto dto, CancellationToken cancellationToken = default)
    {
        var student = await context.Students.Include(s => s.Class)
            .FirstOrDefaultAsync(s => s.Id == dto.StudentId, cancellationToken);

        if (student is null)
            return Result<DisciplinaryCallDto>.NotFound("Aluno não encontrado.");

        var denied = await CheckClassAccessAsync(student.ClassId, student.Class.SchoolId, cancellationToken);
        if (denied is not null)
            return denied;

        var call = new DisciplinaryCall
        {
            StudentId = dto.StudentId,
            Description = dto.Description,
            CreatedById = currentUser.UserId
        };

        await unitOfWork.Repository<DisciplinaryCall>().AddAsync(call, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(call.Id, cancellationToken) is { IsSuccess: true } r
            ? Result<DisciplinaryCallDto>.Created(r.Data!)
            : Result<DisciplinaryCallDto>.BadRequest("Erro ao criar chamado.");
    }

    public async Task<Result<DisciplinaryCallDto>> UpdateAsync(Guid id, UpdateDisciplinaryCallDto dto, CancellationToken cancellationToken = default)
    {
        var call = await context.DisciplinaryCalls
            .Include(d => d.Student).ThenInclude(s => s.Class)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (call is null)
            return Result<DisciplinaryCallDto>.NotFound("Chamado disciplinar não encontrado.");

        if (call.Status != DisciplinaryCallStatus.Pending)
            return Result<DisciplinaryCallDto>.BadRequest("Apenas chamados pendentes podem ser editados.");

        var denied = await CheckClassAccessAsync(call.Student.ClassId, call.Student.Class.SchoolId, cancellationToken);
        if (denied is not null)
            return denied;

        // Professor só edita o que ele mesmo abriu
        if (currentUser.Role == "Teacher" && call.CreatedById is not null && call.CreatedById != currentUser.UserId)
            return Result<DisciplinaryCallDto>.Forbidden("Você só pode editar chamados que você abriu.");

        call.Description = dto.Description;
        call.UpdatedAt = DateTime.UtcNow;

        unitOfWork.Repository<DisciplinaryCall>().Update(call);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<Result<DisciplinaryCallDto>> ApproveAsync(Guid id, Guid resolvedById, ResolveCallDto dto, CancellationToken cancellationToken = default)
        => await ResolveAsync(id, resolvedById, dto, DisciplinaryCallStatus.Approved, cancellationToken);

    public async Task<Result<DisciplinaryCallDto>> RejectAsync(Guid id, Guid resolvedById, ResolveCallDto dto, CancellationToken cancellationToken = default)
        => await ResolveAsync(id, resolvedById, dto, DisciplinaryCallStatus.Rejected, cancellationToken);

    private async Task<Result<DisciplinaryCallDto>> ResolveAsync(Guid id, Guid resolvedById, ResolveCallDto dto, DisciplinaryCallStatus status, CancellationToken cancellationToken)
    {
        var call = await context.DisciplinaryCalls
            .Include(d => d.Student).ThenInclude(s => s.Class)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (call is null)
            return Result<DisciplinaryCallDto>.NotFound("Chamado disciplinar não encontrado.");

        if (call.Status != DisciplinaryCallStatus.Pending)
            return Result<DisciplinaryCallDto>.BadRequest("Chamado já foi resolvido.");

        var denied = await CheckClassAccessAsync(call.Student.ClassId, call.Student.Class.SchoolId, cancellationToken);
        if (denied is not null)
            return denied;

        call.Status = status;
        call.ResolvedById = resolvedById;
        call.ResolvedAt = DateTime.UtcNow;
        call.Resolution = dto.Resolution;
        call.UpdatedAt = DateTime.UtcNow;

        unitOfWork.Repository<DisciplinaryCall>().Update(call);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    // Restringe Diretor à escola e Professor/Orientador às turmas vinculadas
    private async Task<Result<DisciplinaryCallDto>?> CheckClassAccessAsync(Guid classId, Guid schoolId, CancellationToken cancellationToken)
    {
        switch (currentUser.Role)
        {
            case "Director" when schoolId != currentUser.SchoolId:
                return Result<DisciplinaryCallDto>.Forbidden("Este aluno não pertence à sua escola.");

            case "Teacher":
                var isTeacher = await context.TeacherClasses
                    .AnyAsync(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == classId, cancellationToken);
                return isTeacher ? null : Result<DisciplinaryCallDto>.Forbidden("Você não é professor da turma deste aluno.");

            case "Orientador":
                var isOrientador = await context.OrientadorClasses
                    .AnyAsync(oc => oc.OrientadorId == currentUser.UserId && oc.ClassId == classId, cancellationToken);
                return isOrientador ? null : Result<DisciplinaryCallDto>.Forbidden("Você não é orientador da turma deste aluno.");

            default:
                return null;
        }
    }

    private IQueryable<DisciplinaryCall> ScopedCalls() => scope.DisciplinaryCalls().AsNoTracking()
        .Include(d => d.Student).ThenInclude(s => s.Class)
        .Include(d => d.ResolvedBy)
        .Include(d => d.CreatedBy);

    private static DisciplinaryCallDto ToDto(DisciplinaryCall d) =>
        new(d.Id, d.StudentId, d.Student?.Name ?? string.Empty, d.Description, d.Status,
            d.Status.ToString(), d.ResolvedById, d.ResolvedBy?.Name, d.ResolvedAt, d.Resolution, d.CreatedAt,
            d.CreatedById, d.CreatedBy?.Name, d.Student?.ClassId, d.Student?.Class?.Name, d.Student?.Class?.SchoolId);
}
