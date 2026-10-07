namespace EscolaSystemApi.Application.DTOs.Dashboard;

public sealed record AdminStatsDto(
    int TotalSchools,
    int ActiveSchools,
    int TotalUsers,
    int TotalClasses,
    int TotalStudents,
    // Administradores e diretores bloqueados agora por senha errada
    int LockedAccounts);

// Totais já restritos ao que o usuário logado pode ver
public sealed record DashboardStatsDto(
    int TotalClasses,
    int TotalStudents,
    int TotalStaff,
    int PendingDisciplinaryCalls,
    int PendingWorks,
    decimal? AverageGrade,
    decimal? AttendanceRate);

public sealed record ClassReportDto(
    Guid ClassId,
    string ClassName,
    int Year,
    int StudentCount,
    decimal? AverageGrade,
    decimal? AttendanceRate,
    int DisciplinaryCalls,
    int PendingDisciplinaryCalls);
