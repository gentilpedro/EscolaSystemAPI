namespace EscolaSystemApi.Application.DTOs.Students;

public sealed record CreateStudentDto(string Name, string Email, string Registration, DateOnly BirthDate, Guid ClassId);
