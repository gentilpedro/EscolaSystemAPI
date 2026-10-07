using EscolaSystemApi.Application.DTOs.Schools;
using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Application.Interfaces.Repositories;
using EscolaSystemApi.Common;
using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscolaSystemApi.Application.Services;

public class SchoolService(IUnitOfWork unitOfWork, ICurrentUserService currentUser, AppDbContext context) : ISchoolService
{
    public async Task<Result<PagedResult<SchoolDto>>> GetAllAsync(PagedQuery query, CancellationToken cancellationToken = default)
    {
        if (currentUser.Role != "Admin")
        {
            if (currentUser.SchoolId is null)
                return Result<PagedResult<SchoolDto>>.Success(new PagedResult<SchoolDto>([], 1, query.PageSize, 0, 0));

            var school = await unitOfWork.Repository<School>()
                .GetByIdAsync(currentUser.SchoolId.Value, cancellationToken);

            return school is null
                ? Result<PagedResult<SchoolDto>>.NotFound("Escola não encontrada.")
                : Result<PagedResult<SchoolDto>>.Success(
                    new PagedResult<SchoolDto>([ToDto(school)], 1, 1, 1, 1));
        }

        var baseQuery = context.Schools.AsNoTracking();

        var totalCount = await baseQuery.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);

        var schools = await baseQuery
            .OrderBy(x => x.Name)
            .Skip(query.Skip)
            .Take(query.Take)
            .ToListAsync(cancellationToken);

        return Result<PagedResult<SchoolDto>>.Success(
            new PagedResult<SchoolDto>(schools.Select(ToDto), query.Page, query.PageSize, totalCount, totalPages));
    }

    public async Task<Result<SchoolDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (currentUser.Role != "Admin" && currentUser.SchoolId != id)
            return Result<SchoolDto>.Forbidden("Você não tem acesso a esta escola.");

        var school = await unitOfWork.Repository<School>().GetByIdAsync(id, cancellationToken);
        if (school is null)
            return Result<SchoolDto>.NotFound("Escola não encontrada.");

        if (currentUser.Role != "Admin")
            return Result<SchoolDto>.Success(ToDto(school));

        // O admin não vê as pessoas da escola, só quantas perdem ou recuperam o acesso ao desativar ou reativar
        var activeUsers = await context.Users.Where(SchoolMembers.BelongsTo(id)).CountAsync(u => u.IsActive, cancellationToken);
        return Result<SchoolDto>.Success(ToDto(school) with { ActiveUsers = activeUsers });
    }

    public async Task<Result<SchoolDto>> CreateAsync(CreateSchoolDto dto, CancellationToken cancellationToken = default)
    {
        var email = dto.Email.Trim();
        var exists = await unitOfWork.Repository<School>()
            .ExistsAsync(s => s.Email == email, cancellationToken);

        if (exists)
            return Result<SchoolDto>.Conflict("Já existe uma escola com este e-mail.");

        var school = new School
        {
            Name = dto.Name.Trim(),
            Address = dto.Address.Trim(),
            // Telefone gravado sempre no mesmo formato, digitado com ou sem máscara
            Phone = BrazilianPhone.Format(dto.Phone),
            Email = email
        };

        await unitOfWork.Repository<School>().AddAsync(school, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<SchoolDto>.Created(ToDto(school));
    }

    public async Task<Result<SchoolDto>> UpdateAsync(Guid id, UpdateSchoolDto dto, CancellationToken cancellationToken = default)
    {
        if (currentUser.Role == "Director" && currentUser.SchoolId != id)
            return Result<SchoolDto>.Forbidden("Você não tem permissão para editar esta escola.");

        var school = await unitOfWork.Repository<School>().GetByIdAsync(id, cancellationToken);
        if (school is null)
            return Result<SchoolDto>.NotFound("Escola não encontrada.");

        var email = dto.Email.Trim();
        var emailInUse = await context.Schools
            .AnyAsync(x => x.Email == email && x.Id != id, cancellationToken);

        if (emailInUse)
            return Result<SchoolDto>.Conflict("Já existe uma escola com este e-mail.");

        school.Name = dto.Name.Trim();
        school.Address = dto.Address.Trim();
        school.Phone = BrazilianPhone.Format(dto.Phone);
        school.Email = email;
        school.IsActive = dto.IsActive;
        school.UpdatedAt = DateTime.UtcNow;

        unitOfWork.Repository<School>().Update(school);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<SchoolDto>.Success(ToDto(school));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var school = await unitOfWork.Repository<School>().GetByIdAsync(id, cancellationToken);
        if (school is null)
            return Result<bool>.NotFound("Escola não encontrada.");

        var hasDependents = await context.Classes.AnyAsync(c => c.SchoolId == id, cancellationToken)
                            || await context.Users.AnyAsync(u => u.SchoolId == id, cancellationToken)
                            || await context.SchoolMemberships.AnyAsync(m => m.SchoolId == id, cancellationToken);

        if (hasDependents)
            return Result<bool>.Conflict("A escola possui turmas ou usuários vinculados. Desative-a em vez de excluir.");

        unitOfWork.Repository<School>().Remove(school);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.NoContent();
    }

    private static SchoolDto ToDto(School s) =>
        new(s.Id, s.Name, s.Address, s.Phone, s.Email, s.IsActive, s.CreatedAt);
}