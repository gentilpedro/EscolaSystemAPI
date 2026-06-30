namespace EscolaSystemApi.Application.DTOs.Users;

public sealed record CreateUserDto(
    string Name,
    string Email,
    string Password,
    int RoleId,
    Guid? SchoolId,
    Guid? StudentId = null,
    string? Cpf = null,
    string? Phone = null
);