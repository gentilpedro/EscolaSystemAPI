namespace EscolaSystemApi.Application.DTOs.Students;

public sealed record StudentDto(Guid Id, string Name, string Email, string Registration, DateOnly BirthDate, Guid ClassId, string ClassName, bool IsActive, DateTime CreatedAt);
