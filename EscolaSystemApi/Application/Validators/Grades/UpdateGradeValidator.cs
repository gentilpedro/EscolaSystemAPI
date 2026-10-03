using EscolaSystemApi.Application.DTOs.Grades;
using FluentValidation;

namespace EscolaSystemApi.Application.Validators.Grades;

public class UpdateGradeValidator : AbstractValidator<UpdateGradeDto>
{
    public UpdateGradeValidator()
    {
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Value).InclusiveBetween(0, 10);
        RuleFor(x => x.Period).NotEmpty().MaximumLength(50);
    }
}
