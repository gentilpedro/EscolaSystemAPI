using EscolaSystemApi.Domain.Enums;

namespace EscolaSystemApi.Application.DTOs.DisciplinaryCalls;

public sealed record DisciplinaryCallDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string Description,
    DisciplinaryCallStatus Status,
    string StatusName,
    Guid? ResolvedById,
    string? ResolvedByName,
    DateTime? ResolvedAt,
    string? Resolution,
    DateTime CreatedAt);
