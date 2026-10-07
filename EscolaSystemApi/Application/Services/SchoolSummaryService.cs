using EscolaSystemApi.Application.DTOs.Schools;
using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Common;
using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscolaSystemApi.Application.Services;

/// <summary>
/// Números por escola para o administrador. Lê o banco direto (o AccessScope não entrega dados de escola ao admin)
/// e devolve só contagens e o contato da direção.
/// </summary>
public class SchoolSummaryService(AppDbContext context) : ISchoolSummaryService
{
    public async Task<Result<IReadOnlyList<SchoolOverviewDto>>> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var rows = await context.Schools.AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.IsActive,
                HasDirector = context.Users.Any(u => u.RoleId == RoleIds.Director && u.IsActive && u.SchoolId == s.Id),
                ActiveClasses = context.Classes.Count(c => c.SchoolId == s.Id && c.IsActive),
                ActiveStudents = context.Students.Count(st => st.IsActive && st.Class.SchoolId == s.Id),
                ActiveUsers = context.Users.Count(u => u.IsActive
                    && (u.SchoolId == s.Id || u.SchoolMemberships.Any(m => m.SchoolId == s.Id && m.EndedAt == null))),
                LastGrade = context.Grades.Where(g => g.Class.SchoolId == s.Id).Max(g => (DateTime?)g.CreatedAt),
                LastAttendance = context.Attendances.Where(a => a.Class.SchoolId == s.Id).Max(a => (DateTime?)a.CreatedAt)
            })
            .ToListAsync(cancellationToken);

        IReadOnlyList<SchoolOverviewDto> list = rows
            .Select(r => new SchoolOverviewDto(r.Id, r.Name, r.IsActive, r.HasDirector, r.ActiveClasses, r.ActiveStudents, r.ActiveUsers,
                Latest(r.LastGrade, r.LastAttendance)))
            .ToList();
        return Result<IReadOnlyList<SchoolOverviewDto>>.Success(list);
    }

    public async Task<Result<SchoolSummaryDto>> GetSummaryAsync(Guid schoolId, CancellationToken cancellationToken = default)
    {
        var school = await context.Schools.AsNoTracking().FirstOrDefaultAsync(s => s.Id == schoolId, cancellationToken);
        if (school is null)
            return Result<SchoolSummaryDto>.NotFound("Escola não encontrada.");

        var director = await context.Users.AsNoTracking()
            .Where(u => u.RoleId == RoleIds.Director && u.IsActive && u.SchoolId == schoolId)
            .OrderBy(u => u.CreatedAt)
            .Select(u => new SchoolContactDto(u.Id, u.Name, u.Email, u.Phone))
            .FirstOrDefaultAsync(cancellationToken);

        var byRole = await context.Users.AsNoTracking()
            .Where(u => u.IsActive)
            .Where(SchoolMembers.BelongsTo(schoolId))
            .GroupBy(u => u.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        int Count(int roleId) => byRole.Where(r => r.RoleId == roleId).Sum(r => r.Count);

        var lastGrade = await context.Grades.Where(g => g.Class.SchoolId == schoolId).MaxAsync(g => (DateTime?)g.CreatedAt, cancellationToken);
        var lastAttendance = await context.Attendances.Where(a => a.Class.SchoolId == schoolId).MaxAsync(a => (DateTime?)a.CreatedAt, cancellationToken);

        return Result<SchoolSummaryDto>.Success(new SchoolSummaryDto(
            school.Id, school.Name, school.Email, school.Phone, school.Address, school.IsActive, school.CreatedAt,
            director,
            await context.Classes.CountAsync(c => c.SchoolId == schoolId && c.IsActive, cancellationToken),
            await context.Students.CountAsync(st => st.IsActive && st.Class.SchoolId == schoolId, cancellationToken),
            new SchoolUserCountsDto(
                Count(RoleIds.Director), Count(RoleIds.Teacher), Count(RoleIds.Orientador), Count(RoleIds.Parent), Count(RoleIds.Student),
                byRole.Sum(r => r.Count)),
            Latest(lastGrade, lastAttendance)));
    }

    private static DateTime? Latest(DateTime? a, DateTime? b) => a is null ? b : b is null ? a : a > b ? a : b;
}
