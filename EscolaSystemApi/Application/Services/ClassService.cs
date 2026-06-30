using EscolaSystemApi.Application.DTOs.Classes;
using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Application.Interfaces.Repositories;
using EscolaSystemApi.Common;
using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscolaSystemApi.Application.Services;

public class ClassService(IUnitOfWork unitOfWork, AppDbContext context, ICurrentUserService currentUser) : IClassService
{
    public async Task<Result<PagedResult<ClassDto>>> GetAllAsync(PagedQuery query, CancellationToken cancellationToken = default)
    {
        var baseQuery = context.Classes.AsNoTracking().Include(c => c.School);

        var filtered = currentUser.Role switch
        {
            "Admin" => baseQuery,
            "Director" => baseQuery.Where(c => c.SchoolId == currentUser.SchoolId),
            "Teacher" => baseQuery.Where(c => context.TeacherClasses
                .Any(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == c.Id)),
            "Orientador" => baseQuery.Where(c => context.OrientadorClasses
                .Any(oc => oc.OrientadorId == currentUser.UserId && oc.ClassId == c.Id)),
            _ => baseQuery.Where(_ => false)
        };

        var totalCount = await filtered.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);
        var data = await filtered.Skip(query.Skip).Take(query.Take).ToListAsync(cancellationToken);

        return Result<PagedResult<ClassDto>>.Success(
            new PagedResult<ClassDto>(data.Select(ToDto), query.Page, query.PageSize, totalCount, totalPages));
    }

    public async Task<Result<ClassDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = context.Classes.AsNoTracking().Include(c => c.School);

        var filtered = currentUser.Role switch
        {
            "Admin" => query,
            "Director" => query.Where(c => c.SchoolId == currentUser.SchoolId),
            "Teacher" => query.Where(c => context.TeacherClasses
                .Any(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == c.Id)),
            "Orientador" => query.Where(c => context.OrientadorClasses
                .Any(oc => oc.OrientadorId == currentUser.UserId && oc.ClassId == c.Id)),
            _ => query.Where(_ => false)
        };

        var cls = await filtered.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        return cls is null
            ? Result<ClassDto>.NotFound("Turma não encontrada.")
            : Result<ClassDto>.Success(ToDto(cls));
    }

    public async Task<Result<ClassDto>> CreateAsync(CreateClassDto dto, CancellationToken cancellationToken = default)
    {
        if (currentUser.Role == "Director" && dto.SchoolId != currentUser.SchoolId)
            return Result<ClassDto>.Forbidden("Você não pode criar turmas em outra escola.");

        var schoolExists = await unitOfWork.Repository<School>()
            .ExistsAsync(s => s.Id == dto.SchoolId, cancellationToken);

        if (!schoolExists)
            return Result<ClassDto>.NotFound("Escola não encontrada.");

        var cls = new Class { Name = dto.Name, Year = dto.Year, SchoolId = dto.SchoolId };
        await unitOfWork.Repository<Class>().AddAsync(cls, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(cls.Id, cancellationToken) is { IsSuccess: true } result
            ? Result<ClassDto>.Created(result.Data!)
            : Result<ClassDto>.BadRequest("Erro ao criar turma.");
    }

    public async Task<Result<ClassDto>> UpdateAsync(Guid id, UpdateClassDto dto, CancellationToken cancellationToken = default)
    {
        var cls = await context.Classes.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (cls is null)
            return Result<ClassDto>.NotFound("Turma não encontrada.");

        if (currentUser.Role == "Director" && cls.SchoolId != currentUser.SchoolId)
            return Result<ClassDto>.Forbidden("Você não tem permissão para editar esta turma.");

        var schoolExists = await unitOfWork.Repository<School>()
            .ExistsAsync(s => s.Id == dto.SchoolId, cancellationToken);

        if (!schoolExists)
            return Result<ClassDto>.NotFound("Escola não encontrada.");

        cls.Name = dto.Name;
        cls.Year = dto.Year;
        cls.SchoolId = dto.SchoolId;
        cls.IsActive = dto.IsActive;

        unitOfWork.Repository<Class>().Update(cls);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cls = await context.Classes.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (cls is null)
            return Result<bool>.NotFound("Turma não encontrada.");

        if (currentUser.Role == "Director" && cls.SchoolId != currentUser.SchoolId)
            return Result<bool>.Forbidden("Você não tem permissão para remover esta turma.");

        unitOfWork.Repository<Class>().Remove(cls);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.NoContent();
    }

    private static ClassDto ToDto(Class c) =>
        new(c.Id, c.Name, c.Year, c.SchoolId, c.School?.Name ?? string.Empty, c.IsActive, c.CreatedAt);
}