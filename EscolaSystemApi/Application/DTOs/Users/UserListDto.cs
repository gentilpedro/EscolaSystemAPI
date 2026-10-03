namespace EscolaSystemApi.Application.DTOs.Users;

public sealed record UserListDto(
    Guid Id,
    string Name,
    string Email,
    string Role,
    Guid? SchoolId,
    string? SchoolName,
    bool IsActive,
    DateTime CreatedAt,
    string? Cpf = null,
    string? Phone = null,
    Guid? StudentId = null,
    IReadOnlyList<Guid>? ClassIds = null,
    IReadOnlyList<Guid>? StudentIds = null
);
