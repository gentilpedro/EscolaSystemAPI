namespace EscolaSystemApi.Application.DTOs.Classes;

public sealed record UpdateClassDto(string Name, int Year, Guid SchoolId, bool IsActive);
