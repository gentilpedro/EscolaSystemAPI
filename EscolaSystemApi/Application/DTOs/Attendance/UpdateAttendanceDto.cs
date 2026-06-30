namespace EscolaSystemApi.Application.DTOs.Attendance;

public sealed record UpdateAttendanceDto(bool IsPresent, string? Notes);
