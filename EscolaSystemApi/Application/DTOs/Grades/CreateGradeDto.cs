namespace EscolaSystemApi.Application.DTOs.Grades;

public sealed record CreateGradeDto(Guid StudentId, Guid ClassId, string Subject, decimal Value, string Period);
