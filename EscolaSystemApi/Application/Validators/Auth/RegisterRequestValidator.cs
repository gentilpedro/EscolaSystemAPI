using EscolaSystemApi.Application.DTOs.Auth;
using FluentValidation;

namespace EscolaSystemApi.Application.Validators.Auth;

public class RegisterRequestValidator : AbstractValidator<RegisterRequestDto>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Password).StrongPassword();
        RuleFor(x => x.RoleId).Equal(1)
            .WithMessage("Este endpoint cria apenas administradores (RoleId 1). Use /api/users para os demais perfis.");
    }
}
