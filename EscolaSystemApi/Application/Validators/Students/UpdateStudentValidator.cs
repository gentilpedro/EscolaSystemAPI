using EscolaSystemApi.Application.DTOs.Students;
using FluentValidation;

namespace EscolaSystemApi.Application.Validators.Students;

public class UpdateStudentValidator : AbstractValidator<UpdateStudentDto>
{
    public UpdateStudentValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(200).When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Registration).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ClassId).NotEmpty();
        RuleFor(x => x.BirthDate).LessThan(DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage("Data de nascimento inválida.");
    }
}
