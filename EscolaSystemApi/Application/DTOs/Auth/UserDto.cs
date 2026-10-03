namespace EscolaSystemApi.Application.DTOs.Auth;

public sealed record UserDto(
    Guid Id,
    string Name,
    string Email,
    string Role,
    Guid? SchoolId,
    DateTime CreatedAt,
    string? SchoolName = null,
    Guid? StudentId = null,
    string? Phone = null);
