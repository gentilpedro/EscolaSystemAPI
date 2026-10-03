using EscolaSystemApi.Application.DTOs.Users;
using FluentValidation;

namespace EscolaSystemApi.Application.Validators.Users;

public class UpdateUserValidator : AbstractValidator<UpdateUserDto>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.RoleId).InclusiveBetween(1, 6).WithMessage("RoleId inválido.");
        RuleFor(x => x.Cpf).Matches(@"^\d{3}\.?\d{3}\.?\d{3}-?\d{2}$").WithMessage("CPF inválido.").When(x => !string.IsNullOrWhiteSpace(x.Cpf));
        RuleFor(x => x.Phone).MaximumLength(20);
    }
}
