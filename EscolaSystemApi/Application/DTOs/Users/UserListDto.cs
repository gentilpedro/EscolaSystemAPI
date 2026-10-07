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
    IReadOnlyList<Guid>? StudentIds = null,
    // Escolas em que a pessoa tem vínculo ativo (professor, orientador e responsável podem ter várias)
    IReadOnlyList<SchoolRefDto>? Schools = null,
    // Até quando a conta fica bloqueada por senha errada; null quando não está bloqueada
    DateTime? LockedUntil = null
);

public sealed record SchoolRefDto(Guid Id, string Name);
