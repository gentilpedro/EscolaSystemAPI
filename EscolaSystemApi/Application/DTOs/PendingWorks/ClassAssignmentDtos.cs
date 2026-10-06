namespace EscolaSystemApi.Application.DTOs.PendingWorks;

// Trabalho lançado para todos os alunos ativos da turma
public sealed record CreateClassAssignmentDto(Guid ClassId, string Title, string Description, DateOnly DueDate);

// Correção do trabalho em todos os alunos; as entregas já registradas não mudam
public sealed record UpdateAssignmentDto(string Title, string Description, DateOnly DueDate);

public sealed record ClassAssignmentDto(
    Guid AssignmentId,
    Guid ClassId,
    string ClassName,
    string Title,
    string Description,
    DateOnly DueDate,
    int StudentCount,
    int DeliveredCount);
