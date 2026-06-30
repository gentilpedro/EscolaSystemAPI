namespace EscolaSystemApi.Application.DTOs.Classes;

public sealed record CreateClassDto(string Name, int Year, Guid SchoolId);
