using EscolaSystemApi.Application.DTOs.Dashboard;
using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Common;
using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Domain.Enums;
using EscolaSystemApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscolaSystemApi.Application.Services;

public class DashboardService(AppDbContext context, ICurrentUserService currentUser) : IDashboardService
{
    public async Task<Result<AdminStatsDto>> GetAdminStatsAsync(CancellationToken cancellationToken = default)
    {
        var stats = new AdminStatsDto(
            await context.Schools.CountAsync(cancellationToken),
            await context.Schools.CountAsync(s => s.IsActive, cancellationToken),
            await context.Users.CountAsync(u => u.IsActive, cancellationToken),
            await context.Classes.CountAsync(c => c.IsActive, cancellationToken),
            await context.Students.CountAsync(s => s.IsActive, cancellationToken));

        return Result<AdminStatsDto>.Success(stats);
    }

    public async Task<Result<DashboardStatsDto>> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var classes = VisibleClasses();
        var students = VisibleStudents();
        var studentIds = students.Select(s => s.Id);

        var grades = context.Grades.Where(g => studentIds.Contains(g.StudentId));
        var attendances = context.Attendances.Where(a => studentIds.Contains(a.StudentId));

        var totalAttendances = await attendances.CountAsync(cancellationToken);
        var presents = await attendances.CountAsync(a => a.IsPresent, cancellationToken);
        var averageGrade = await grades.AverageAsync(g => (decimal?)g.Value, cancellationToken);

        var stats = new DashboardStatsDto(
            await classes.CountAsync(c => c.IsActive, cancellationToken),
            await students.CountAsync(s => s.IsActive, cancellationToken),
            await VisibleStaff().CountAsync(cancellationToken),
            await context.DisciplinaryCalls.CountAsync(
                d => d.Status == DisciplinaryCallStatus.Pending && studentIds.Contains(d.StudentId), cancellationToken),
            await context.PendingWorks.CountAsync(p => !p.IsDelivered && studentIds.Contains(p.StudentId), cancellationToken),
            averageGrade is null ? null : Math.Round(averageGrade.Value, 2),
            totalAttendances == 0 ? null : Math.Round(presents * 100m / totalAttendances, 1));

        return Result<DashboardStatsDto>.Success(stats);
    }

    public async Task<Result<List<ClassReportDto>>> GetClassReportsAsync(Guid? schoolId = null, CancellationToken cancellationToken = default)
    {
        var classes = VisibleClasses();
        if (schoolId.HasValue)
            classes = classes.Where(c => c.SchoolId == schoolId.Value);

        var classList = await classes
            .OrderByDescending(c => c.Year).ThenBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.Year })
            .ToListAsync(cancellationToken);

        var classIds = classList.Select(c => c.Id).ToList();

        // Uma consulta agregada por indicador, em vez de uma por turma
        var studentCounts = await context.Students
            .Where(s => classIds.Contains(s.ClassId) && s.IsActive)
            .GroupBy(s => s.ClassId)
            .Select(g => new { ClassId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClassId, x => x.Count, cancellationToken);

        var gradeAverages = await context.Grades
            .Where(g => classIds.Contains(g.ClassId))
            .GroupBy(g => g.ClassId)
            .Select(g => new { ClassId = g.Key, Average = g.Average(x => x.Value) })
            .ToDictionaryAsync(x => x.ClassId, x => x.Average, cancellationToken);

        var attendanceStats = await context.Attendances
            .Where(a => classIds.Contains(a.ClassId))
            .GroupBy(a => a.ClassId)
            .Select(g => new { ClassId = g.Key, Total = g.Count(), Present = g.Count(x => x.IsPresent) })
            .ToDictionaryAsync(x => x.ClassId, cancellationToken);

        var disciplinaryStats = await context.DisciplinaryCalls
            .Where(d => classIds.Contains(d.Student.ClassId))
            .GroupBy(d => d.Student.ClassId)
            .Select(g => new
            {
                ClassId = g.Key,
                Total = g.Count(),
                Pending = g.Count(x => x.Status == DisciplinaryCallStatus.Pending)
            })
            .ToDictionaryAsync(x => x.ClassId, cancellationToken);

        var reports = classList.Select(c =>
        {
            attendanceStats.TryGetValue(c.Id, out var attendance);
            disciplinaryStats.TryGetValue(c.Id, out var disciplinary);

            return new ClassReportDto(
                c.Id,
                c.Name,
                c.Year,
                studentCounts.GetValueOrDefault(c.Id),
                gradeAverages.TryGetValue(c.Id, out var avg) ? Math.Round(avg, 2) : null,
                attendance is { Total: > 0 } ? Math.Round(attendance.Present * 100m / attendance.Total, 1) : null,
                disciplinary?.Total ?? 0,
                disciplinary?.Pending ?? 0);
        }).ToList();

        return Result<List<ClassReportDto>>.Success(reports);
    }

    private IQueryable<Class> VisibleClasses() => currentUser.Role switch
    {
        "Admin" => context.Classes,
        "Director" => context.Classes.Where(c => c.SchoolId == currentUser.SchoolId),
        "Teacher" => context.Classes.Where(c => context.TeacherClasses
            .Any(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == c.Id)),
        "Orientador" => context.Classes.Where(c => context.OrientadorClasses
            .Any(oc => oc.OrientadorId == currentUser.UserId && oc.ClassId == c.Id)),
        "Student" => context.Classes.Where(c => context.Students
            .Any(s => s.Id == currentUser.StudentId && s.ClassId == c.Id)),
        "Parent" => context.Classes.Where(c => context.ParentStudents
            .Any(ps => ps.ParentId == currentUser.UserId && ps.Student.ClassId == c.Id)),
        _ => context.Classes.Where(_ => false)
    };

    private IQueryable<Student> VisibleStudents() => currentUser.Role switch
    {
        "Admin" => context.Students,
        "Director" => context.Students.Where(s => s.Class.SchoolId == currentUser.SchoolId),
        "Teacher" => context.Students.Where(s => context.TeacherClasses
            .Any(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == s.ClassId)),
        "Orientador" => context.Students.Where(s => context.OrientadorClasses
            .Any(oc => oc.OrientadorId == currentUser.UserId && oc.ClassId == s.ClassId)),
        "Student" => context.Students.Where(s => s.Id == currentUser.StudentId),
        "Parent" => context.Students.Where(s => context.ParentStudents
            .Any(ps => ps.ParentId == currentUser.UserId && ps.StudentId == s.Id)),
        _ => context.Students.Where(_ => false)
    };

    // Funcionários: diretores, professores e orientadores ativos
    private IQueryable<User> VisibleStaff()
    {
        int[] staffRoles = [RoleIds.Director, RoleIds.Teacher, RoleIds.Orientador];
        var staff = context.Users.Where(u => u.IsActive && staffRoles.Contains(u.RoleId));

        return currentUser.Role switch
        {
            "Admin" => staff,
            "Director" => staff.Where(u => u.SchoolId == currentUser.SchoolId),
            _ => staff.Where(_ => false)
        };
    }
}
