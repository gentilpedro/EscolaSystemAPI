using EscolaSystemApi.Application.DTOs.Classes;
using FluentValidation;

namespace EscolaSystemApi.Application.Validators.Classes;

public class CreateClassValidator : AbstractValidator<CreateClassDto>
{
    public CreateClassValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.SchoolId).NotEmpty();
    }
}
