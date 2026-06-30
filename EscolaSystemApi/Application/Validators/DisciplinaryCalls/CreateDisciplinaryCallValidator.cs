using EscolaSystemApi.Application.DTOs.DisciplinaryCalls;
using FluentValidation;

namespace EscolaSystemApi.Application.Validators.DisciplinaryCalls;

public class CreateDisciplinaryCallValidator : AbstractValidator<CreateDisciplinaryCallDto>
{
    public CreateDisciplinaryCallValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.Description).NotEmpty().MaximumLength(1000);
    }
}
