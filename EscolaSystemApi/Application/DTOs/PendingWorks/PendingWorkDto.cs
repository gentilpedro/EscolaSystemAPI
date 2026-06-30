namespace EscolaSystemApi.Application.DTOs.PendingWorks;

public sealed record PendingWorkDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    Guid ClassId,
    string ClassName,
    string Title,
    string Description,
    DateOnly DueDate,
    bool IsDelivered,
    DateTime? DeliveredAt,
    DateTime CreatedAt);
