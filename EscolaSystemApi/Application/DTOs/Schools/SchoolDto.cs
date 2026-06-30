namespace EscolaSystemApi.Application.DTOs.Schools;

public sealed record SchoolDto(Guid Id, string Name, string Address, string Phone, string Email, bool IsActive, DateTime CreatedAt);
