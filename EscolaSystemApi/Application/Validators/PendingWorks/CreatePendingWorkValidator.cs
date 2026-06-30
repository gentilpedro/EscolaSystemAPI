using EscolaSystemApi.Application.DTOs.PendingWorks;
using FluentValidation;

namespace EscolaSystemApi.Application.Validators.PendingWorks;

public class CreatePendingWorkValidator : AbstractValidator<CreatePendingWorkDto>
{
    public CreatePendingWorkValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.ClassId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
    }
}
