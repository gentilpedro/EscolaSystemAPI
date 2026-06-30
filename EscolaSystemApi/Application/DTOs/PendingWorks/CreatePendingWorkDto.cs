namespace EscolaSystemApi.Application.DTOs.PendingWorks;

public sealed record CreatePendingWorkDto(Guid StudentId, Guid ClassId, string Title, string Description, DateOnly DueDate);
