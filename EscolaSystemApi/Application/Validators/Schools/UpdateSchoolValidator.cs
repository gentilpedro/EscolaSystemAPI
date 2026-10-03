using EscolaSystemApi.Application.DTOs.Schools;
using FluentValidation;

namespace EscolaSystemApi.Application.Validators.Schools;

public class UpdateSchoolValidator : AbstractValidator<UpdateSchoolDto>
{
    public UpdateSchoolValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Phone).MaximumLength(20);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
    }
}
