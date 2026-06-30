using EscolaSystemApi.Application.DTOs.Auth;
using FluentValidation;

namespace EscolaSystemApi.Application.Validators.Auth;

public class ResetPasswordDtoValidator : AbstractValidator<ResetPasswordDto>
{
    public ResetPasswordDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(200);

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(100)
            .Matches(@"[A-Z]").WithMessage("A senha deve conter ao menos uma letra maiúscula.")
            .Matches(@"[a-z]").WithMessage("A senha deve conter ao menos uma letra minúscula.")
            .Matches(@"[0-9]").WithMessage("A senha deve conter ao menos um número.")
            .Matches(@"[^a-zA-Z0-9]").WithMessage("A senha deve conter ao menos um caractere especial.");
    }
}
