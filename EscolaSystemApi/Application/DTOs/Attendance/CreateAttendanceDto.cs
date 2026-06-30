namespace EscolaSystemApi.Application.DTOs.Attendance;

public sealed record CreateAttendanceDto(Guid StudentId, Guid ClassId, DateOnly Date, bool IsPresent, string? Notes);
