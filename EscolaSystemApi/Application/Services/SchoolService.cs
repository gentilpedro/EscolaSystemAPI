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
        if (currentUser.Role == "Director")
        {
            var school = await unitOfWork.Repository<School>()
                .GetByIdAsync(currentUser.SchoolId!.Value, cancellationToken);

            return school is null
                ? Result<PagedResult<SchoolDto>>.NotFound("Escola não encontrada.")
                : Result<PagedResult<SchoolDto>>.Success(
                    new PagedResult<SchoolDto>([ToDto(school)], 1, 1, 1, 1));
        }

        var baseQuery = context.Schools.AsNoTracking();

        var totalCount = await baseQuery.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);

        var schools = await baseQuery
            .Skip(query.Skip)
            .Take(query.Take)
            .ToListAsync(cancellationToken);

        return Result<PagedResult<SchoolDto>>.Success(
            new PagedResult<SchoolDto>(schools.Select(ToDto), query.Page, query.PageSize, totalCount, totalPages));
    }

    public async Task<Result<SchoolDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (currentUser.Role == "Director" && currentUser.SchoolId != id)
            return Result<SchoolDto>.Forbidden("Você não tem acesso a esta escola.");

        var school = await unitOfWork.Repository<School>().GetByIdAsync(id, cancellationToken);
        return school is null
            ? Result<SchoolDto>.NotFound("Escola não encontrada.")
            : Result<SchoolDto>.Success(ToDto(school));
    }

    public async Task<Result<SchoolDto>> CreateAsync(CreateSchoolDto dto, CancellationToken cancellationToken = default)
    {
        var exists = await unitOfWork.Repository<School>()
            .ExistsAsync(s => s.Email == dto.Email, cancellationToken);

        if (exists)
            return Result<SchoolDto>.Conflict("Já existe uma escola com este e-mail.");

        var school = new School
        {
            Name = dto.Name,
            Address = dto.Address,
            Phone = dto.Phone,
            Email = dto.Email
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

        school.Name = dto.Name;
        school.Address = dto.Address;
        school.Phone = dto.Phone;
        school.Email = dto.Email;
        school.IsActive = dto.IsActive;

        unitOfWork.Repository<School>().Update(school);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<SchoolDto>.Success(ToDto(school));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var school = await unitOfWork.Repository<School>().GetByIdAsync(id, cancellationToken);
        if (school is null)
            return Result<bool>.NotFound("Escola não encontrada.");

        unitOfWork.Repository<School>().Remove(school);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.NoContent();
    }

    private static SchoolDto ToDto(School s) =>
        new(s.Id, s.Name, s.Address, s.Phone, s.Email, s.IsActive, s.CreatedAt);
}