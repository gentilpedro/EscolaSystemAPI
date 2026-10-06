using EscolaSystemApi.Application.DTOs.Auth;
using FluentValidation;

namespace EscolaSystemApi.Application.Validators.Auth;

public class ChangePasswordDtoValidator : AbstractValidator<ChangePasswordDto>
{
    public ChangePasswordDtoValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Informe a senha atual.");
        RuleFor(x => x.NewPassword).StrongPassword();
    }
}
