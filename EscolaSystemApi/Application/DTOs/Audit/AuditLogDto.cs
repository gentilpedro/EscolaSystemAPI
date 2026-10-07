namespace EscolaSystemApi.Application.DTOs.Audit;

public sealed record AuditLogDto(
    Guid Id,
    DateTime CreatedAt,
    Guid? ActorId,
    string ActorName,
    string Action,
    string TargetType,
    Guid TargetId,
    string TargetName,
    string? Details);
