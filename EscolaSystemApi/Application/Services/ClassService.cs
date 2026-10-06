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
    // Regras de visibilidade por perfil (quem enxerga o quê) ficam no AccessScope
    private readonly AccessScope scope = new(context, currentUser);

    public async Task<Result<PagedResult<ClassDto>>> GetAllAsync(PagedQuery query, Guid? schoolId = null, CancellationToken cancellationToken = default)
    {
        var filtered = ScopedClasses();

        if (schoolId.HasValue)
            filtered = filtered.Where(c => c.SchoolId == schoolId.Value);

        var totalCount = await filtered.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);
        var data = await filtered
            .OrderByDescending(c => c.Year).ThenBy(c => c.Name)
            .Skip(query.Skip).Take(query.Take)
            .ToListAsync(cancellationToken);

        return Result<PagedResult<ClassDto>>.Success(
            new PagedResult<ClassDto>(data.Select(ToDto), query.Page, query.PageSize, totalCount, totalPages));
    }

    public async Task<Result<ClassDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cls = await ScopedClasses().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

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

        var duplicate = await context.Classes
            .AnyAsync(c => c.SchoolId == dto.SchoolId && c.Year == dto.Year && c.Name == dto.Name, cancellationToken);

        if (duplicate)
            return Result<ClassDto>.Conflict("Já existe uma turma com este nome neste ano.");

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

        if (currentUser.Role == "Director" && (cls.SchoolId != currentUser.SchoolId || dto.SchoolId != currentUser.SchoolId))
            return Result<ClassDto>.Forbidden("Você não tem permissão para editar esta turma ou movê-la para outra escola.");

        if (dto.SchoolId != cls.SchoolId)
        {
            var schoolExists = await unitOfWork.Repository<School>()
                .ExistsAsync(s => s.Id == dto.SchoolId, cancellationToken);

            if (!schoolExists)
                return Result<ClassDto>.NotFound("Escola não encontrada.");

            // Professores, orientadores e alunos ficariam vinculados a uma turma de outra escola
            var hasLinks = await context.Students.AnyAsync(s => s.ClassId == id, cancellationToken)
                           || await context.TeacherClasses.AnyAsync(tc => tc.ClassId == id, cancellationToken)
                           || await context.OrientadorClasses.AnyAsync(oc => oc.ClassId == id, cancellationToken);

            if (hasLinks)
                return Result<ClassDto>.Conflict("Não é possível mudar a escola de uma turma com alunos ou profissionais vinculados.");
        }

        var duplicate = await context.Classes
            .AnyAsync(c => c.Id != id && c.SchoolId == dto.SchoolId && c.Year == dto.Year && c.Name == dto.Name, cancellationToken);

        if (duplicate)
            return Result<ClassDto>.Conflict("Já existe uma turma com este nome neste ano.");

        cls.Name = dto.Name;
        cls.Year = dto.Year;
        cls.SchoolId = dto.SchoolId;
        cls.IsActive = dto.IsActive;
        cls.UpdatedAt = DateTime.UtcNow;

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

        var hasHistory = await context.Students.AnyAsync(s => s.ClassId == id, cancellationToken)
                         || await context.Grades.AnyAsync(g => g.ClassId == id, cancellationToken)
                         || await context.Attendances.AnyAsync(a => a.ClassId == id, cancellationToken)
                         || await context.PendingWorks.AnyAsync(p => p.ClassId == id, cancellationToken);

        if (hasHistory)
            return Result<bool>.Conflict("A turma possui alunos ou histórico (notas, chamadas, trabalhos). Desative-a em vez de excluir.");

        unitOfWork.Repository<Class>().Remove(cls);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.NoContent();
    }

    private IQueryable<Class> ScopedClasses() => scope.Classes().AsNoTracking().Include(c => c.School);

    private static ClassDto ToDto(Class c) =>
        new(c.Id, c.Name, c.Year, c.SchoolId, c.School?.Name ?? string.Empty, c.IsActive, c.CreatedAt);
}
