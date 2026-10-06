using System.Text.RegularExpressions;
using EscolaSystemApi.Common;
using FluentValidation;

namespace EscolaSystemApi.Application.Validators.Schools;

// Regras comuns ao cadastro e à edição de escola
public static partial class SchoolRules
{
    // nome@dominio.tld: o EmailAddress() do FluentValidation aceita "escola@x"
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s.]{2,}$")]
    private static partial Regex EmailPattern();

    public static IRuleBuilderOptions<T, string> SchoolName<T>(this IRuleBuilder<T, string> rule) => rule
        .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Informe o nome da escola.")
        .Must(v => v is null || v.Trim().Length >= 3).WithMessage("O nome da escola precisa ter ao menos 3 caracteres.")
        .MaximumLength(300).WithMessage("O nome da escola pode ter até 300 caracteres.");

    public static IRuleBuilderOptions<T, string> SchoolAddress<T>(this IRuleBuilder<T, string> rule) => rule
        .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Informe o endereço da escola.")
        .Must(v => v is null || v.Trim().Length >= 5).WithMessage("O endereço precisa ter ao menos 5 caracteres.")
        .MaximumLength(500).WithMessage("O endereço pode ter até 500 caracteres.");

    public static IRuleBuilderOptions<T, string> SchoolPhone<T>(this IRuleBuilder<T, string> rule) => rule
        .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Informe o telefone da escola.")
        .Must(v => string.IsNullOrWhiteSpace(v) || BrazilianPhone.IsValid(v))
        .WithMessage("Telefone inválido. Use DDD e número, ex.: (51) 3333-1200 ou (51) 99999-1200.");

    public static IRuleBuilderOptions<T, string> SchoolEmail<T>(this IRuleBuilder<T, string> rule) => rule
        .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Informe o e-mail da escola.")
        .Must(v => string.IsNullOrWhiteSpace(v) || EmailPattern().IsMatch(v.Trim()))
        .WithMessage("E-mail inválido. Use o formato nome@dominio.com.br.")
        .MaximumLength(200).WithMessage("O e-mail pode ter até 200 caracteres.");
}
