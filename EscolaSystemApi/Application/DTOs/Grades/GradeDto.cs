namespace EscolaSystemApi.Application.DTOs.Grades;

public sealed record GradeDto(Guid Id, Guid StudentId, string StudentName, Guid ClassId, string ClassName, string Subject, decimal Value, string Period, DateTime CreatedAt);
