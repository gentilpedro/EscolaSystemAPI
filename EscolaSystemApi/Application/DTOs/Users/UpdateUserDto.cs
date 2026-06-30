namespace EscolaSystemApi.Application.DTOs.Users;

public sealed record UpdateUserDto(
    string Name,
    string Email,
    int RoleId,
    Guid? SchoolId,
    bool IsActive,
    string? Cpf = null,
    string? Phone = null
);