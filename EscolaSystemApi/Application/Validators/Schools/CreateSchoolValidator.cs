using EscolaSystemApi.Application.DTOs.Schools;
using FluentValidation;

namespace EscolaSystemApi.Application.Validators.Schools;

public class CreateSchoolValidator : AbstractValidator<CreateSchoolDto>
{
    public CreateSchoolValidator()
    {
        RuleFor(x => x.Name).SchoolName();
        RuleFor(x => x.Address).SchoolAddress();
        RuleFor(x => x.Phone).SchoolPhone();
        RuleFor(x => x.Email).SchoolEmail();
    }
}
