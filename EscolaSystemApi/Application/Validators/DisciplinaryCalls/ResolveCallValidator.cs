using EscolaSystemApi.Application.DTOs.DisciplinaryCalls;
using FluentValidation;

namespace EscolaSystemApi.Application.Validators.DisciplinaryCalls;

public class ResolveCallValidator : AbstractValidator<ResolveCallDto>
{
    public ResolveCallValidator()
    {
        RuleFor(x => x.Resolution).MaximumLength(1000);
    }
}
