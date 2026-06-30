namespace EscolaSystemApi.Application.DTOs.DisciplinaryCalls;

public sealed record CreateDisciplinaryCallDto(Guid StudentId, string Description);
