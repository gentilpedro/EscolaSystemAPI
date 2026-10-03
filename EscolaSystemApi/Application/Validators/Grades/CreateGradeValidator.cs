using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Application.DTOs.Grades;
using FluentValidation;

namespace EscolaSystemApi.Application.Validators.Grades;

public class CreateGradeValidator : AbstractValidator<CreateGradeDto>
{
    public CreateGradeValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.ClassId).NotEmpty();
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Value).InclusiveBetween(0, 10);
        RuleFor(x => x.Period).NotEmpty().MaximumLength(50)
            .Must(GradePeriods.IsValid)
            .WithMessage("Período inválido. Use: " + string.Join(", ", GradePeriods.All) + ".");
    }
}
