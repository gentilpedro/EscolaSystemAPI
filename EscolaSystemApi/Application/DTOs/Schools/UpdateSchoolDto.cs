namespace EscolaSystemApi.Application.DTOs.Schools;

public sealed record UpdateSchoolDto(string Name, string Address, string Phone, string Email, bool IsActive);
