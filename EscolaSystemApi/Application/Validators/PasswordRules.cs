using FluentValidation;

namespace EscolaSystemApi.Application.Validators;

public static class PasswordRules
{
    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(100)
            .Matches(@"[A-Z]").WithMessage("A senha deve conter ao menos uma letra maiúscula.")
            .Matches(@"[a-z]").WithMessage("A senha deve conter ao menos uma letra minúscula.")
            .Matches(@"[0-9]").WithMessage("A senha deve conter ao menos um número.")
            .Matches(@"[^a-zA-Z0-9]").WithMessage("A senha deve conter ao menos um caractere especial.");
}
