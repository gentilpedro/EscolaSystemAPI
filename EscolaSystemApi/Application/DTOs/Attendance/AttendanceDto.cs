namespace EscolaSystemApi.Application.DTOs.Attendance;

public sealed record AttendanceDto(Guid Id, Guid StudentId, string StudentName, Guid ClassId, string ClassName, DateOnly Date, bool IsPresent, string? Notes, DateTime CreatedAt);
