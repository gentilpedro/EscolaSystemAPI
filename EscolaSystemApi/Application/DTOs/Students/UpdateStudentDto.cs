namespace EscolaSystemApi.Application.DTOs.Students;

public sealed record UpdateStudentDto(string Name, string Email, string Registration, DateOnly BirthDate, Guid ClassId, bool IsActive);
