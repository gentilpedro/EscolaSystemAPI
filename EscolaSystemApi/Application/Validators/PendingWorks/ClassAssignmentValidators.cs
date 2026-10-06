using EscolaSystemApi.Application.DTOs.PendingWorks;
using FluentValidation;

namespace EscolaSystemApi.Application.Validators.PendingWorks;

public class CreateClassAssignmentValidator : AbstractValidator<CreateClassAssignmentDto>
{
    public CreateClassAssignmentValidator()
    {
        RuleFor(x => x.ClassId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.DueDate).NotEmpty();
    }
}

public class UpdateAssignmentValidator : AbstractValidator<UpdateAssignmentDto>
{
    public UpdateAssignmentValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.DueDate).NotEmpty();
    }
}
