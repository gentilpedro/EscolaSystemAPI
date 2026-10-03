using EscolaSystemApi.Application.DTOs.DisciplinaryCalls;
using FluentValidation;

namespace EscolaSystemApi.Application.Validators.DisciplinaryCalls;

public class UpdateDisciplinaryCallValidator : AbstractValidator<UpdateDisciplinaryCallDto>
{
    public UpdateDisciplinaryCallValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(1000);
    }
}
