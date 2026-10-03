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
    DateTime CreatedAt,
    Guid? CreatedById = null,
    string? CreatedByName = null,
    Guid? ClassId = null,
    string? ClassName = null,
    Guid? SchoolId = null);

public sealed record DisciplinaryCallFilter(
    Guid? SchoolId = null,
    Guid? StudentId = null,
    Guid? ClassId = null,
    DisciplinaryCallStatus? Status = null);
