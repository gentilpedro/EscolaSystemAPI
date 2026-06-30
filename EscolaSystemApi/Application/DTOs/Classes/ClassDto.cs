namespace EscolaSystemApi.Application.DTOs.Classes;

public sealed record ClassDto(Guid Id, string Name, int Year, Guid SchoolId, string SchoolName, bool IsActive, DateTime CreatedAt);
