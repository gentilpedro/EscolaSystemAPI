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
    public async Task<Result<PagedResult<DisciplinaryCallDto>>> GetAllAsync(PagedQuery query, CancellationToken cancellationToken = default)
    {
        var baseQuery = context.DisciplinaryCalls.AsNoTracking()
            .Include(d => d.Student).ThenInclude(s => s.Class).ThenInclude(c => c.School)
            .Include(d => d.ResolvedBy);

        var filtered = currentUser.Role switch
        {
            "Admin" => baseQuery,
            "Director" => baseQuery.Where(d => d.Student.Class.SchoolId == currentUser.SchoolId),
            "Teacher" => baseQuery.Where(d => context.TeacherClasses
                .Any(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == d.Student.ClassId)),
            "Orientador" => baseQuery.Where(d => context.OrientadorClasses
                .Any(oc => oc.OrientadorId == currentUser.UserId && oc.ClassId == d.Student.ClassId)),
            "Student" => baseQuery.Where(d => d.StudentId == currentUser.StudentId),
            "Parent" => baseQuery.Where(d => context.ParentStudents
                .Any(ps => ps.ParentId == currentUser.UserId && ps.StudentId == d.StudentId)),
            _ => baseQuery.Where(_ => false)
        };

        var totalCount = await filtered.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);
        var data = await filtered.Skip(query.Skip).Take(query.Take).ToListAsync(cancellationToken);

        return Result<PagedResult<DisciplinaryCallDto>>.Success(
            new PagedResult<DisciplinaryCallDto>(data.Select(ToDto), query.Page, query.PageSize, totalCount, totalPages));
    }

    public async Task<Result<DisciplinaryCallDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = context.DisciplinaryCalls.AsNoTracking()
            .Include(d => d.Student).ThenInclude(s => s.Class).ThenInclude(c => c.School)
            .Include(d => d.ResolvedBy);

        var filtered = currentUser.Role switch
        {
            "Admin" => query,
            "Director" => query.Where(d => d.Student.Class.SchoolId == currentUser.SchoolId),
            "Teacher" => query.Where(d => context.TeacherClasses
                .Any(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == d.Student.ClassId)),
            "Orientador" => query.Where(d => context.OrientadorClasses
                .Any(oc => oc.OrientadorId == currentUser.UserId && oc.ClassId == d.Student.ClassId)),
            "Student" => query.Where(d => d.StudentId == currentUser.StudentId),
            "Parent" => query.Where(d => context.ParentStudents
                .Any(ps => ps.ParentId == currentUser.UserId && ps.StudentId == d.StudentId)),
            _ => query.Where(_ => false)
        };

        var call = await filtered.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
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

        if (currentUser.Role == "Teacher")
        {
            var isTeacherOfClass = await context.TeacherClasses
                .AnyAsync(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == student.ClassId, cancellationToken);

            if (!isTeacherOfClass)
                return Result<DisciplinaryCallDto>.Forbidden("Você não é professor da turma deste aluno.");
        }

        if (currentUser.Role == "Orientador")
        {
            var isOrientadorOfClass = await context.OrientadorClasses
                .AnyAsync(oc => oc.OrientadorId == currentUser.UserId && oc.ClassId == student.ClassId, cancellationToken);

            if (!isOrientadorOfClass)
                return Result<DisciplinaryCallDto>.Forbidden("Você não é orientador da turma deste aluno.");
        }

        if (currentUser.Role == "Director" && student.Class.SchoolId != currentUser.SchoolId)
            return Result<DisciplinaryCallDto>.Forbidden("Este aluno não pertence à sua escola.");

        var call = new DisciplinaryCall
        {
            StudentId = dto.StudentId,
            Description = dto.Description
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

        if (currentUser.Role == "Teacher")
        {
            var isTeacherOfClass = await context.TeacherClasses
                .AnyAsync(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == call.Student.ClassId, cancellationToken);

            if (!isTeacherOfClass)
                return Result<DisciplinaryCallDto>.Forbidden("Você não é professor da turma deste aluno.");
        }

        if (currentUser.Role == "Orientador")
        {
            var isOrientadorOfClass = await context.OrientadorClasses
                .AnyAsync(oc => oc.OrientadorId == currentUser.UserId && oc.ClassId == call.Student.ClassId, cancellationToken);

            if (!isOrientadorOfClass)
                return Result<DisciplinaryCallDto>.Forbidden("Você não é orientador da turma deste aluno.");
        }

        if (currentUser.Role == "Director" && call.Student.Class.SchoolId != currentUser.SchoolId)
            return Result<DisciplinaryCallDto>.Forbidden("Este chamado não pertence à sua escola.");

        call.Description = dto.Description;
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

        if (currentUser.Role == "Director" && call.Student.Class.SchoolId != currentUser.SchoolId)
            return Result<DisciplinaryCallDto>.Forbidden("Este chamado não pertence à sua escola.");

        call.Status = status;
        call.ResolvedById = resolvedById;
        call.ResolvedAt = DateTime.UtcNow;
        call.Resolution = dto.Resolution;

        unitOfWork.Repository<DisciplinaryCall>().Update(call);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    private static DisciplinaryCallDto ToDto(DisciplinaryCall d) =>
        new(d.Id, d.StudentId, d.Student?.Name ?? string.Empty, d.Description, d.Status,
            d.Status.ToString(), d.ResolvedById, d.ResolvedBy?.Name, d.ResolvedAt, d.Resolution, d.CreatedAt);
}