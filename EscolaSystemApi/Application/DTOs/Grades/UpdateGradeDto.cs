namespace EscolaSystemApi.Application.DTOs.Grades;

public sealed record UpdateGradeDto(string Subject, decimal Value, string Period);
